using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Services;

/// <summary>Outcome of provisioning this service's tenant schema.</summary>
public record ProvisioningResult(bool Success, string Schema, string? Error);

/// <summary>
/// Creates and migrates this service's tables into a tenant's Postgres schema. Business services
/// hold no control plane, so this just CREATE SCHEMAs and migrates — status tracking lives in the
/// user-service control plane, which reads the result the orchestrator relays.
/// </summary>
public interface ITenantProvisioningService
{
    Task<ProvisioningResult> ProvisionAsync(string schema, CancellationToken cancellationToken = default);
}

public partial class TenantProvisioningService : ITenantProvisioningService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(IConfiguration configuration, ILogger<TenantProvisioningService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ProvisioningResult> ProvisionAsync(string schema, CancellationToken cancellationToken = default)
    {
        if (!IsSafeSchema(schema))
            return new ProvisioningResult(false, schema, $"Unsafe schema name '{schema}'.");

        try
        {
            await using var ctx = BuildTenantContext(schema);
#pragma warning disable EF1002 // schema is validated by IsSafeSchema/SafeSchemaRegex above before use — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            await ctx.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", cancellationToken);
#pragma warning restore EF1002
            await ctx.Database.MigrateAsync(cancellationToken);
            await SeedReferenceDataAsync(ctx, cancellationToken);
            var grantError = await GrantAppRoleAsync(ctx, schema, cancellationToken);

            if (grantError != null)
            {
                _logger.LogError("Provisioned schema {Schema} but role grant failed: {Error}", schema, grantError);
                return new ProvisioningResult(false, schema, grantError);
            }

            _logger.LogInformation("Provisioned ticketing schema {Schema}", schema);
            return new ProvisioningResult(true, schema, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed provisioning ticketing schema {Schema}", schema);
            return new ProvisioningResult(false, schema, ex.Message);
        }
    }

    /// <summary>Seeds sensible starter reference data (categories, SLA tiers, tags) into a freshly
    /// provisioned tenant schema. Idempotent — skips if categories already exist. Categories are
    /// company-wide (DepartmentId unset); admins can bind them to departments later.</summary>
    private static async Task SeedReferenceDataAsync(TenantTicketingDbContext ctx, CancellationToken ct)
    {
        if (await ctx.TicketCategories.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;
        // D1-5 — the 11 Module-7-spec helpdesk categories (fixed ids so the portal + downstream logic
        // can reference them reliably; seeded identically into every tenant, like user-service system
        // roles). IsComplaint drives the complaint workflow (D5); BusinessHoursOnly=false = 24/7 clock
        // (Emergency, IT-P1). Fine-grained per-category SLA hours are tuned in D2-1; here each category
        // inherits the standard per-priority tiers below via its DefaultPriority.
        var categories = new[]
        {
            new TicketCategory { Id = "cat-emergency",         Name = "Emergency",          Description = "Life/safety or critical business-stopping incidents", DefaultPriority = TicketPriority.Critical, BusinessHoursOnly = false, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new TicketCategory { Id = "cat-technical-support", Name = "Technical Support",  Description = "Product/service technical assistance",                DefaultPriority = TicketPriority.High,     IsActive = true, CreatedAt = now, UpdatedAt = now },
            new TicketCategory { Id = "cat-invoice-query",     Name = "Invoice Query",      Description = "Billing, invoice and payment queries",                DefaultPriority = TicketPriority.Medium,   IsActive = true, CreatedAt = now, UpdatedAt = now },
            new TicketCategory { Id = "cat-certificate-query", Name = "Certificate Query",  Description = "Calibration/test certificate queries and reissues",   DefaultPriority = TicketPriority.Medium,   IsActive = true, CreatedAt = now, UpdatedAt = now },
            new TicketCategory { Id = "cat-service-request",   Name = "Service Request",    Description = "Requests for a service, quotation or field work",     DefaultPriority = TicketPriority.Medium,   IsActive = true, CreatedAt = now, UpdatedAt = now },
            new TicketCategory { Id = "cat-complaint",         Name = "Complaint",          Description = "External client complaints and escalations",          DefaultPriority = TicketPriority.High,     IsComplaint = true, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new TicketCategory { Id = "cat-general-enquiry",   Name = "General Enquiry",    Description = "General questions and other requests",                DefaultPriority = TicketPriority.Low,      IsActive = true, CreatedAt = now, UpdatedAt = now },
            // Internal IT helpdesk P1–P4 (P1 runs 24/7). SLA targets tightened in D2-1/D7.
            new TicketCategory { Id = "cat-it-p1", Name = "IT Support — P1 (Critical)", Description = "IT: critical outage — 24/7",       DefaultPriority = TicketPriority.Critical, BusinessHoursOnly = false, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new TicketCategory { Id = "cat-it-p2", Name = "IT Support — P2 (High)",     Description = "IT: major impact",                DefaultPriority = TicketPriority.High,     IsActive = true, CreatedAt = now, UpdatedAt = now },
            new TicketCategory { Id = "cat-it-p3", Name = "IT Support — P3 (Medium)",   Description = "IT: moderate impact",             DefaultPriority = TicketPriority.Medium,   IsActive = true, CreatedAt = now, UpdatedAt = now },
            new TicketCategory { Id = "cat-it-p4", Name = "IT Support — P4 (Low)",      Description = "IT: minor request",               DefaultPriority = TicketPriority.Low,      IsActive = true, CreatedAt = now, UpdatedAt = now },
            // SR-portal categories (PublicServiceRequestController hardcodes these ids). Previously
            // unseeded — a fresh tenant's SR portal 404'd on /verify; seeding them here closes that gap.
            new TicketCategory { Id = "cat-technical-service", Name = "Technical Service", Description = "Calibration/field technical service request", DefaultPriority = TicketPriority.High, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new TicketCategory { Id = "cat-calibration-nawi",  Name = "Calibration — NAWI", Description = "Non-automatic weighing instrument calibration", DefaultPriority = TicketPriority.High, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new TicketCategory { Id = "cat-calibration-mass",  Name = "Calibration — Mass", Description = "Mass/weights calibration",                     DefaultPriority = TicketPriority.High, IsActive = true, CreatedAt = now, UpdatedAt = now },
        };
        await ctx.TicketCategories.AddRangeAsync(categories, ct);

        // Standard SLA tiers (response/resolution hours) applied per category per priority.
        var slaTiers = new (TicketPriority Priority, int Response, int Resolution)[]
        {
            (TicketPriority.Critical, 1, 4),
            (TicketPriority.High,     4, 24),
            (TicketPriority.Medium,   8, 72),
            (TicketPriority.Low,     24, 168),
        };
        foreach (var cat in categories)
            foreach (var tier in slaTiers)
                await ctx.SLAPolicies.AddAsync(new SLAPolicy
                {
                    CategoryId = cat.Id, Priority = tier.Priority,
                    ResponseTimeHours = tier.Response, ResolutionTimeHours = tier.Resolution,
                    CreatedAt = now, UpdatedAt = now
                }, ct);

        // D7-1 — tighten the internal IT-helpdesk P1–P4 resolution SLAs to spec
        // (P1 1h & 24/7, P2 4h, P3 8h, P4 24h) on each category's default priority.
        var itTargets = new (string Category, TicketPriority Priority, int Resolution)[]
        {
            ("cat-it-p1", TicketPriority.Critical, 1),
            ("cat-it-p2", TicketPriority.High,     4),
            ("cat-it-p3", TicketPriority.Medium,   8),
            ("cat-it-p4", TicketPriority.Low,      24),
        };
        foreach (var it in itTargets)
        {
            var policy = ctx.SLAPolicies.Local
                .FirstOrDefault(p => p.CategoryId == it.Category && p.Priority == it.Priority);
            if (policy != null) policy.ResolutionTimeHours = it.Resolution;
        }

        var tags = new[]
        {
            new Tag { Name = "Urgent",    Color = "#ef4444", Description = "Requires immediate attention", CreatedAt = now, UpdatedAt = now },
            new Tag { Name = "Follow-up", Color = "#f59e0b", Description = "Needs a follow-up",            CreatedAt = now, UpdatedAt = now },
            new Tag { Name = "Billable",  Color = "#22c55e", Description = "Chargeable work",              CreatedAt = now, UpdatedAt = now },
            new Tag { Name = "Warranty",  Color = "#3b82f6", Description = "Covered under warranty",       CreatedAt = now, UpdatedAt = now },
            new Tag { Name = "Escalated", Color = "#a855f7", Description = "Escalated to management",      CreatedAt = now, UpdatedAt = now },
        };
        await ctx.Tags.AddRangeAsync(tags, ct);

        await ctx.SaveChangesAsync(ct);
    }

    private TenantTicketingDbContext BuildTenantContext(string schema)
    {
        var baseConnectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

        var connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schema
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<TenantTicketingDbContext>()
            .UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly("TicketingService.Infrastructure");
                // Pin the history table to the tenant schema so EF's existence check and its SELECT
                // both target the same schema (an unqualified check can resolve to public.__EFMigrationsHistory).
                o.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            })
            // The migration snapshot may differ from the runtime model (e.g. legacy drift); the
            // schema is bound via search_path, so silence EF9's pending-changes guard during Migrate.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new TenantTicketingDbContext(options);
    }

    /// <summary>
    /// Grants the runtime app role (qalicore_app) access to the freshly provisioned schema. Without
    /// this, search_path silently drops the schema and queries fall through to public. Best-effort.
    /// </summary>
    private async Task<string?> GrantAppRoleAsync(TenantTicketingDbContext ctx, string schema, CancellationToken ct)
    {
        var appRole = _configuration["ProvisioningAppRole"] ?? "qalicore_app";
        if (!SafeSchemaRegex().IsMatch(appRole)) return null;
        try
        {
#pragma warning disable EF1002 // appRole is validated by SafeSchemaRegex above before use — not user input, and identifiers can't be parameterized via SqlQuery/ExecuteSqlAsync anyway.
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
            _logger.LogError(ex, "Failed to grant {Role} on {Schema}; tenant may be unreadable at runtime.", appRole, schema);
            return ex.Message;
        }
    }

    private static bool IsSafeSchema(string schema)
        => !string.IsNullOrWhiteSpace(schema) && SafeSchemaRegex().IsMatch(schema);

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex SafeSchemaRegex();
}
