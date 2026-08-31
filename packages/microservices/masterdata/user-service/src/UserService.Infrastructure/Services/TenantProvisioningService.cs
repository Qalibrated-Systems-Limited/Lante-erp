using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UserService.Core.Constants;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Services;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Services;

/// <summary>
/// See <see cref="ITenantProvisioningService"/>. Builds a schema-bound <see cref="TenantDbContext"/>
/// on the fly, creates the schema, migrates the tenant-plane tables into it (each schema gets its own
/// <c>__EFMigrationsHistory</c>), seeds standard data, and drives the control-plane tracker row.
/// </summary>
public partial class TenantProvisioningService : ITenantProvisioningService
{
    private readonly LanteUserServiceDbContext _controlPlane;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(
        LanteUserServiceDbContext controlPlane,
        IConfiguration configuration,
        ILogger<TenantProvisioningService> logger)
    {
        _controlPlane = controlPlane;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ProvisioningResult> ProvisionUserSchemaAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _controlPlane.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
            return new ProvisioningResult(false, PlatformServices.User, string.Empty, "Tenant not found.");

        var schema = tenant.SchemaName;
        if (!IsSafeSchema(schema))
            return new ProvisioningResult(false, PlatformServices.User, schema, $"Unsafe schema name '{schema}'.");

        // Control-plane tracker row for this (tenant, user-service).
        var tracker = await _controlPlane.TenantServiceSchemas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(ts => ts.TenantId == tenantId && ts.ServiceKey == PlatformServices.User, cancellationToken);
        if (tracker is null)
        {
            tracker = new TenantServiceSchema
            {
                TenantId = tenantId,
                ServiceKey = PlatformServices.User,
                SchemaName = schema,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _controlPlane.TenantServiceSchemas.Add(tracker);
        }

        tracker.Status = ProvisioningStatus.Provisioning;
        tracker.SchemaName = schema;
        tracker.LastError = null;
        tracker.UpdatedAt = DateTime.UtcNow;
        await _controlPlane.SaveChangesAsync(cancellationToken);

        string? grantError = null;
        try
        {
            await using var tenantCtx = BuildTenantContext(schema);

            // Serialize concurrent provisioning attempts for the same schema — e.g. multiple
            // pods' startup seeders racing each other, or a startup seeder overlapping a
            // manual admin-triggered provision call. Without this, two callers can both pass
            // SeedTenantAsync's check-then-act idempotency guard before either commits, and
            // whichever inserts second hits a duplicate-key violation.
            await using var lockConn = new NpgsqlConnection(GetBaseConnectionString());
            await lockConn.OpenAsync(cancellationToken);
            var lockKey = $"user-service-provision:{schema}";
            await using (var lockCmd = lockConn.CreateCommand())
            {
                lockCmd.CommandText = "SELECT pg_advisory_lock(hashtextextended(@key, 0))";
                lockCmd.Parameters.AddWithValue("key", lockKey);
                await lockCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            try
            {
                // Create the schema, then migrate the tenant-plane tables into it. EF puts
                // __EFMigrationsHistory in the model's default schema (= this tenant schema).
#pragma warning disable EF1002 // schema is validated by IsSafeSchema above before use — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
                await tenantCtx.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", cancellationToken);
#pragma warning restore EF1002
                await tenantCtx.Database.MigrateAsync(cancellationToken);
                grantError = await GrantAppRoleAsync(tenantCtx, schema, cancellationToken);

                // EnableRetryOnFailure is on for this DbContext (Program.cs). SeedTenantAsync's
                // Add()+SaveChangesAsync isn't wrapped in an execution strategy, so a transient
                // blip during commit makes EF retry the whole save — but the entities it already
                // added to the tracker on the failed attempt are still tracked as Added, so the
                // retry re-submits them alongside the ones it just added again, colliding with
                // rows that may have actually committed on the "failed" attempt (duplicate-key on
                // Departments/Roles). Run it inside the execution strategy and clear the tracker
                // on each attempt so retries start from a clean, idempotent slate.
                var seedStrategy = tenantCtx.Database.CreateExecutionStrategy();
                await seedStrategy.ExecuteAsync(async () =>
                {
                    tenantCtx.ChangeTracker.Clear();
                    await SeedTenantAsync(tenantCtx, tenant, cancellationToken);
                });
            }
            finally
            {
                await using var unlockCmd = lockConn.CreateCommand();
                unlockCmd.CommandText = "SELECT pg_advisory_unlock(hashtextextended(@key, 0))";
                unlockCmd.Parameters.AddWithValue("key", lockKey);
                await unlockCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            if (grantError != null)
            {
                _logger.LogError("Provisioned user-service schema {Schema} for tenant {TenantId} but role grant failed: {Error}", schema, tenantId, grantError);
                tracker.Status = ProvisioningStatus.Failed;
                tracker.LastError = Truncate(grantError, 1000);
                tracker.UpdatedAt = DateTime.UtcNow;
                await _controlPlane.SaveChangesAsync(cancellationToken);
                return new ProvisioningResult(false, PlatformServices.User, schema, grantError);
            }

            tracker.Status = ProvisioningStatus.Provisioned;
            tracker.ProvisionedAt = DateTime.UtcNow;
            tracker.LastError = null;
            tracker.UpdatedAt = DateTime.UtcNow;
            await _controlPlane.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Provisioned user-service schema {Schema} for tenant {TenantId}", schema, tenantId);
            return new ProvisioningResult(true, PlatformServices.User, schema, null);
        }
        catch (Exception ex)
        {
            var detail = ex is DbUpdateException { InnerException: PostgresException pgEx }
                ? $"{pgEx.SqlState}: {pgEx.MessageText}"
                : ex.Message;
            _logger.LogError(ex, "Failed provisioning user-service schema {Schema} for tenant {TenantId}", schema, tenantId);
            tracker.Status = ProvisioningStatus.Failed;
            tracker.LastError = Truncate(detail, 1000);
            tracker.UpdatedAt = DateTime.UtcNow;
            await _controlPlane.SaveChangesAsync(cancellationToken);
            return new ProvisioningResult(false, PlatformServices.User, schema, detail);
        }
    }

    /// <summary>
    /// Builds a TenantDbContext whose connection is pinned to the tenant schema via Postgres
    /// <c>search_path</c>. Unqualified DDL/DML and the <c>__EFMigrationsHistory</c> table therefore
    /// resolve into that schema. Each schema gets a distinct connection string (distinct pool).
    /// </summary>
    private string GetBaseConnectionString()
        => _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

    private TenantDbContext BuildTenantContext(string schema)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(GetBaseConnectionString())
        {
            SearchPath = schema
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly("UserService.Infrastructure");
                // Pin the history table to the tenant schema so EF's existence check and its SELECT
                // both target the same schema (an unqualified check can resolve to public.__EFMigrationsHistory).
                o.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            })
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new TenantDbContext(options);
    }

    /// <summary>
    /// Seeds standard tenant-plane data. Idempotent: skips if the schema already has users.
    /// Copies the HQ branch + company-admin from the control plane so the schema mirrors the
    /// tenant's current public-plane state (Phase 5 reconciles the two).
    /// </summary>
    private async Task SeedTenantAsync(TenantDbContext ctx, Tenant tenant, CancellationToken ct)
    {
        if (await ctx.Users.IgnoreQueryFilters().AnyAsync(ct))
        {
            _logger.LogInformation("Schema {Schema} already seeded; skipping.", tenant.SchemaName);
            // Tenants seeded before a permission was added to the global catalog (e.g. licensing.*)
            // never get it, since this whole method is skipped once the schema has users. Backfill
            // role-admin on every provision call so already-provisioned tenants stay in sync with
            // the "Admin gets the full non-platform catalog" rule below.
            await EnsureAdminHasFullPermissionsAsync(ctx, tenant, ct);
            return;
        }

        var now = DateTime.UtcNow;

        // 1. Standard, per-tenant roles. Company-wide roles (Admin/MD/Executive) are locked
        // (IsSystem=true). Department roles are seeded editable (IsSystem=false) so each company
        // admin can fine-tune their permissions in the Roles UI.
        var roleDefs = new (string Id, string Name, string Desc, bool System)[]
        {
            ("role-admin",          "Admin",             "Company administrator — full access",                 true),
            ("role-md",             "MD",                "Managing Director — org-wide oversight & approvals",  true),
            ("role-executive",      "Executive",         "Board/executive — read-only oversight",               true),
            ("role-finance-mgr",    "Finance Manager",   "Finance department manager",                          false),
            ("role-finance-staff",  "Finance Staff",     "Finance department staff",                            false),
            ("role-hr-mgr",         "HR Manager",        "Human Resources manager",                             false),
            ("role-hr-staff",       "HR Staff",          "Human Resources staff",                               false),
            ("role-technical-mgr",  "Technical Manager", "Technical department manager",                        false),
            ("role-technical-staff","Technical Staff",   "Technical department staff",                          false),
            ("role-fleet-mgr",      "Fleet Manager",     "Fleet department manager",                            false),
            ("role-fleet-staff",    "Fleet Staff",       "Fleet department staff",                              false),
            ("role-it-mgr",         "ICT Manager",       "ICT department manager",                              false),
            ("role-it-staff",       "ICT Staff",         "ICT department staff",                                false),
            ("role-rnd-mgr",        "R&D Manager",       "Research & Development manager",                       false),
            ("role-rnd-staff",      "R&D Staff",         "Research & Development staff",                         false),
            ("role-sales-mgr",      "Sales Manager",     "Sales department manager",                            false),
            ("role-sales-staff",    "Sales Staff",       "Sales department staff",                              false),
            ("role-employee",       "Employee",          "Standard employee",                                   false),
        };
        foreach (var r in roleDefs)
            ctx.Roles.Add(new Role
            {
                Id = r.Id, Name = r.Name, Description = r.Desc, TenantId = tenant.Id,
                IsActive = true, IsSystem = r.System, CreatedAt = now, UpdatedAt = now
            });

        // 1b. Role → permission assignments, keyed by permission NAME and resolved against the global
        // public.Permissions catalog. "role-admin" gets the full non-platform catalog; the rest get a
        // sensible department default. platform.* permissions are control-plane only and never granted.
        var rolePermissionNames = new Dictionary<string, string[]>
        {
            // "licensing.read"/"licensing.write" deliberately omitted from every role below: that
            // permission name is also checked by the separate license-service to gate its
            // cross-customer SaaS license catalog, not any feature these tenant roles actually
            // have (nothing in fleet-service enforces it for the driver/equipment-license view it
            // was meant for). Granting it here let any tenant with these roles read/revoke every
            // OTHER customer's software license, since license-service trusts the same JWT
            // signing key as user-service. See DatabaseSeeder.cs's PermPlatformLicensing.
            ["role-md"] = new[] {
                "users.read",
                "tickets.read.all","tickets.assign","tickets.resolve",
                "projects.read.all","projects.approve",
                "finance.read","finance.approve","finance.reports",
                "reports.view","reports.export","portal.manage",
                "fleet.read","fleet.expenses",
                "technician.read","technician.approve",
                "operations.read.own","operations.approve"
            },
            ["role-executive"] = new[] {
                "users.read",
                "tickets.read.all","projects.read.all",
                "finance.read","finance.reports",
                "fleet.read","technician.read","operations.read.own",
                "reports.view","reports.export"
            },
            ["role-finance-mgr"] = new[] {
                "users.read",
                "finance.read","finance.write","finance.approve","finance.reports",
                "projects.read.all","tickets.read.dept",
                "fleet.expenses","technician.read","operations.read.own",
                "reports.view","reports.export"
            },
            ["role-finance-staff"] = new[] {
                "finance.read","finance.write","tickets.read.own","tickets.write","reports.view"
            },
            ["role-hr-mgr"] = new[] {
                "users.read","users.write","departments.manage",
                "tickets.read.dept","tickets.assign","reports.view","reports.export"
            },
            ["role-hr-staff"] = new[] {
                "users.read","tickets.read.own","tickets.write"
            },
            ["role-technical-mgr"] = new[] {
                "projects.read.dept","projects.write","projects.delete",
                "tickets.read.dept","tickets.write","tickets.assign","tickets.resolve",
                "finance.read",
                "technician.read","technician.write","technician.approve",
                "operations.read.own","operations.write","operations.approve",
                "reports.view","reports.export"
            },
            ["role-technical-staff"] = new[] {
                "projects.read.own","tickets.read.own","tickets.write","operations.read.own"
            },
            ["role-fleet-mgr"] = new[] {
                "fleet.read","fleet.write","fleet.delete","fleet.expenses","fleet.dispatch.request",
                "tickets.read.dept","tickets.write","tickets.assign",
                "reports.view","reports.export",
                "users.read"
            },
            ["role-fleet-staff"] = new[] {
                "fleet.read","fleet.write","tickets.read.own","tickets.write"
            },
            ["role-it-mgr"] = new[] {
                "users.read","users.write","users.delete",
                "roles.manage","permissions.manage","departments.manage","settings.manage",
                "tickets.read.all","tickets.write","tickets.assign","tickets.resolve","tickets.delete",
                "fleet.read","technician.read","operations.read.own",
                "reports.view","reports.export"
            },
            ["role-it-staff"] = new[] {
                "tickets.read.dept","tickets.write"
            },
            ["role-rnd-mgr"] = new[] {
                "projects.read.dept","projects.write",
                "tickets.read.dept","tickets.write","tickets.assign","tickets.resolve",
                "technician.read","operations.read.own",
                "reports.view","reports.export"
            },
            ["role-rnd-staff"] = new[] {
                "projects.read.own","tickets.read.own","tickets.write","operations.read.own"
            },
            ["role-sales-mgr"] = new[] {
                "portal.manage","tickets.read.dept","tickets.write","tickets.assign",
                "reports.view","reports.export"
            },
            ["role-sales-staff"] = new[] {
                "portal.manage","tickets.read.own","tickets.write"
            },
            ["role-employee"] = new[] {
                "tickets.read.own","tickets.write","operations.read.own"
            },
        };

        // Catalog = global permissions minus platform.* (those are control-plane only).
        var catalog = await _controlPlane.Permissions
            .Where(p => !p.Name.StartsWith("platform."))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);
        var nameToId = catalog.ToDictionary(p => p.Name, p => p.Id);

        foreach (var role in roleDefs)
        {
            // Admin gets everything; others get their mapped default (empty if none defined).
            var permNames = role.Id == "role-admin"
                ? catalog.Select(c => c.Name)
                : (rolePermissionNames.TryGetValue(role.Id, out var m) ? m : Array.Empty<string>());
            foreach (var permName in permNames.Distinct())
            {
                if (!nameToId.TryGetValue(permName, out var pid)) continue;
                ctx.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id, PermissionId = pid, AssignedAt = now, CreatedAt = now, UpdatedAt = now
                });
            }
        }

        // 2. Departments — one per functional area (managers/staff align to these).
        var deptNames = new[] { "Management", "Finance", "Human Resources", "Technical", "Fleet", "ICT", "R&D", "Sales", "General" };
        foreach (var dn in deptNames)
            ctx.Departments.Add(new Department
            {
                Name = dn, Description = $"{dn} department", TenantId = tenant.Id,
                IsActive = true, CreatedAt = now, UpdatedAt = now
            });

        // 3. HQ branch — copied from the control-plane HQ if present, else a sensible default.
        var cpBranch = await _controlPlane.Branches.IgnoreQueryFilters()
            .Where(b => b.TenantId == tenant.Id && b.IsHeadOffice)
            .OrderBy(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);
        ctx.Branches.Add(new Branch
        {
            // Reuse the control-plane branch's Id so it stays valid as a foreign key when
            // UserDirectory later writes UserTenants rows (pinned to the public schema) with
            // this BranchId.
            Id = cpBranch?.Id ?? Guid.NewGuid().ToString(),
            TenantId = tenant.Id,
            Name = cpBranch?.Name ?? "Head Office",
            Code = cpBranch?.Code ?? "HQ",
            IsHeadOffice = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        // 4. Company-admin user — copied from the control-plane admin (UserTenant with BranchId == null).
        var cpAdmin = await _controlPlane.UserTenants.IgnoreQueryFilters()
            .Include(ut => ut.User)
            .Where(ut => ut.TenantId == tenant.Id && ut.BranchId == null)
            .Select(ut => ut.User)
            .FirstOrDefaultAsync(ct);

        if (cpAdmin is not null)
        {
            var admin = new User
            {
                FirstName = cpAdmin.FirstName,
                LastName = cpAdmin.LastName,
                Email = cpAdmin.Email,
                MobileNumber = cpAdmin.MobileNumber,
                Password = cpAdmin.Password,      // preserve BCrypt hash so login works post Phase 3
                AuthProvider = cpAdmin.AuthProvider,
                IsActive = true,
                IsCompanyAdmin = true,   // the seeded admin is the company administrator
                IsFirstLogin = cpAdmin.IsFirstLogin,
                TwoFactorEnabled = cpAdmin.TwoFactorEnabled,
                CreatedAt = now,
                UpdatedAt = now
            };
            ctx.Users.Add(admin);
            ctx.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = "role-admin", CreatedAt = now, UpdatedAt = now });
        }
        else
        {
            _logger.LogWarning("No control-plane admin found for tenant {TenantId}; schema seeded without an admin user.", tenant.Id);
        }

        await ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Grants role-admin any global (non-platform.*) permission it's missing. Additive/idempotent —
    /// existing grants (including ones an admin has since revoked) are left untouched.
    /// </summary>
    private async Task EnsureAdminHasFullPermissionsAsync(TenantDbContext ctx, Tenant tenant, CancellationToken ct)
    {
        var catalog = await _controlPlane.Permissions
            .Where(p => !p.Name.StartsWith("platform."))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        var existingIds = await ctx.RolePermissions.IgnoreQueryFilters()
            .Where(rp => rp.RoleId == "role-admin")
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);
        var existingSet = existingIds.ToHashSet();

        var now = DateTime.UtcNow;
        var missing = catalog.Where(p => !existingSet.Contains(p.Id)).ToList();
        if (missing.Count == 0) return;

        foreach (var perm in missing)
            ctx.RolePermissions.Add(new RolePermission
            {
                RoleId = "role-admin", PermissionId = perm.Id, AssignedAt = now, CreatedAt = now, UpdatedAt = now
            });

        await ctx.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Backfilled {Count} missing role-admin permission(s) for schema {Schema}: {Perms}",
            missing.Count, tenant.SchemaName, string.Join(", ", missing.Select(p => p.Name)));
    }

    /// <summary>Grants the runtime app role (qalicore_app) access to the provisioned schema so
    /// search_path resolves it at request time once the main DbContext moves off the admin
    /// connection. Returns null on success, or an error message — callers must treat a non-null
    /// return as provisioning failure, not a best-effort warning.</summary>
    private async Task<string?> GrantAppRoleAsync(TenantDbContext ctx, string schema, CancellationToken ct)
    {
        var appRole = _configuration["ProvisioningAppRole"] ?? "qalicore_app";
        if (!SafeSchemaRegex().IsMatch(appRole)) return $"Configured app role '{appRole}' failed safety check.";
        try
        {
#pragma warning disable EF1002 // appRole is validated by SafeSchemaRegex above before use — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            var roleExists = await ctx.Database
                .SqlQueryRaw<int>($"SELECT 1 AS \"Value\" FROM pg_roles WHERE rolname = '{appRole}'")
                .AnyAsync(ct);
#pragma warning restore EF1002
            if (!roleExists)
            {
                return $"App role '{appRole}' not found.";
            }
#pragma warning disable EF1002 // schema and appRole are validated by IsSafeSchema/SafeSchemaRegex above before use — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            await ctx.Database.ExecuteSqlRawAsync($"GRANT USAGE ON SCHEMA \"{schema}\" TO {appRole}", ct);
            await ctx.Database.ExecuteSqlRawAsync($"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA \"{schema}\" TO {appRole}", ct);
            await ctx.Database.ExecuteSqlRawAsync($"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA \"{schema}\" TO {appRole}", ct);
            await ctx.Database.ExecuteSqlRawAsync($"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{schema}\" GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {appRole}", ct);
#pragma warning restore EF1002
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to grant {Role} on {Schema}; tenant would be unreadable at runtime once AppConnection is wired.", appRole, schema);
            return ex.Message;
        }
    }

    private static bool IsSafeSchema(string schema)
        => !string.IsNullOrWhiteSpace(schema) && SafeSchemaRegex().IsMatch(schema);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex SafeSchemaRegex();
}
