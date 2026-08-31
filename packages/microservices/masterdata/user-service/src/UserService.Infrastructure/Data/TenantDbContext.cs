using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;

namespace UserService.Infrastructure.Data;

/// <summary>
/// Tenant-plane DbContext: the tables that live in each tenant's own Postgres schema
/// (<c>tenant_&lt;slug&gt;</c>) rather than the shared <c>public</c> control plane.
///
/// <para>
/// The model is intentionally schema-AGNOSTIC (no <c>HasDefaultSchema</c>): the tenant schema is
/// bound at the connection level via Postgres <c>search_path</c>. Provisioning sets it through the
/// connection string; Phase 3's request-time interceptor will set it per request. Because the model
/// carries no default schema, the SAME migration applies into any tenant schema — unqualified DDL,
/// the <c>__EFMigrationsHistory</c> table, and all queries resolve against whatever <c>search_path</c>
/// the connection carries.
/// </para>
///
/// <para>
/// Control-plane entities (Tenant, SubscriptionPlan, CompanySubscription, TenantServiceSchema) and
/// the global RBAC catalog (Permission, RolePermission, UserPermission) are intentionally NOT part
/// of this context. Permissions/role-permissions land in Phase 4; UserTenant collapses in Phase 3.
/// They are Ignore()d here so EF does not pull the whole graph in via navigations.
/// </para>
/// </summary>
public class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<Branch> Branches { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;
    public DbSet<PersonalAccessToken> PersonalAccessTokens { get; set; } = null!;
    public DbSet<PasswordPolicy> PasswordPolicies { get; set; } = null!;
    public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<SystemModule> SystemModules { get; set; } = null!;
    public DbSet<DocumentTemplate> DocumentTemplates { get; set; } = null!;
    // Phase 4 RBAC: per-tenant role/user permission assignments live in the tenant schema.
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<UserPermission> UserPermissions { get; set; } = null!;
    // Global permission catalog — physically in public; mapped here (ExcludeFromMigrations) only so
    // the per-tenant RolePermission/UserPermission FKs can reference it cross-schema.
    public DbSet<Permission> Permissions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // No HasDefaultSchema: the tenant schema is bound via the connection's search_path,
        // keeping this model (and its migration) schema-agnostic and reusable across all tenants.

        // ── User ────────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            b.HasKey(u => u.Id);
            b.Property(u => u.Id).HasMaxLength(36);
            b.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
            b.Property(u => u.LastName).IsRequired().HasMaxLength(100);
            b.Property(u => u.Email).IsRequired().HasMaxLength(255);
            b.Property(u => u.Password).IsRequired(false).HasMaxLength(512);
            b.Property(u => u.MobileNumber).HasMaxLength(20);
            b.HasIndex(u => u.Email).IsUnique().HasDatabaseName("IX_Users_Email");

            b.HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasMany(u => u.UserRoles)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(u => u.PersonalAccessTokens)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(u => u.UserPermissions)
                .WithOne(up => up.User)
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Control-plane navigation — not part of the tenant plane (collapsed in Phase 3).
            b.Ignore(u => u.UserTenants);
        });

        // ── Role ────────────────────────────────────────────────────────────────
        modelBuilder.Entity<Role>(b =>
        {
            b.ToTable("Roles");
            b.HasKey(r => r.Id);
            b.Property(r => r.Id).HasMaxLength(36);
            b.Property(r => r.Name).IsRequired().HasMaxLength(100);
            b.Property(r => r.Description).HasMaxLength(500);
            // Unique per-schema (i.e. per-tenant) which is exactly what we want.
            b.HasIndex(r => r.Name).IsUnique().HasDatabaseName("IX_Roles_Name");

            b.HasMany(r => r.UserRoles)
                .WithOne(ur => ur.Role)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(r => r.RolePermissions)
                .WithOne(rp => rp.Role)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Department ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Department>(b =>
        {
            b.ToTable("Departments");
            b.HasKey(d => d.Id);
            b.Property(d => d.Id).HasMaxLength(36);
            b.Property(d => d.Name).IsRequired().HasMaxLength(200);
            b.Property(d => d.Description).HasMaxLength(500);
            b.HasIndex(d => d.Name).IsUnique().HasDatabaseName("IX_Departments_Name");
        });

        // ── Branch ──────────────────────────────────────────────────────────────
        // In the tenant plane every branch implicitly belongs to THE tenant (the schema),
        // so we keep TenantId as a plain column but drop the control-plane Tenant FK/navs.
        modelBuilder.Entity<Branch>(b =>
        {
            b.ToTable("Branches");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(36);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Code).HasMaxLength(50);
            b.Ignore(x => x.Tenant);
            b.Ignore(x => x.UserTenants);
        });

        // ── UserRole (join) ───────────────────────────────────────────────────────
        modelBuilder.Entity<UserRole>(b =>
        {
            b.ToTable("UserRoles");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(36);
        });

        // ── PersonalAccessToken ────────────────────────────────────────────────────
        modelBuilder.Entity<PersonalAccessToken>(b =>
        {
            b.ToTable("PersonalAccessTokens");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(36);
            // Stores the full generated JWT (TokenService.CreateJwt), which grows with the user's
            // role/permission/department claim count — RoleAdmin alone carries ~35 permission
            // claims and comfortably exceeds 2048 chars, throwing Postgres 22001 on login for any
            // richly-permissioned user (confirmed live via the google-login 500).
            b.Property(x => x.Token).HasColumnType("text");
        });

        // ── PasswordPolicy ─────────────────────────────────────────────────────────
        modelBuilder.Entity<PasswordPolicy>(b =>
        {
            b.ToTable("PasswordPolicies");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(36);
        });

        // ── SystemSetting ─────────────────────────────────────────────────────────
        modelBuilder.Entity<SystemSetting>(b =>
        {
            b.ToTable("SystemSettings");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(36);
            b.HasIndex(x => x.Key).IsUnique().HasDatabaseName("IX_SystemSettings_Key");
        });

        // ── AuditLog ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("AuditLogs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(36);
            b.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_AuditLogs_CreatedAt");
        });

        // ── SystemModule ──────────────────────────────────────────────────────────
        modelBuilder.Entity<SystemModule>(b =>
        {
            b.ToTable("SystemModules");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(36);
            b.HasIndex(x => x.ModuleKey).IsUnique().HasDatabaseName("IX_SystemModules_ModuleKey");
        });

        // ── DocumentTemplate ──────────────────────────────────────────────────────
        modelBuilder.Entity<DocumentTemplate>(b =>
        {
            b.ToTable("DocumentTemplates");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(36);
            b.HasIndex(x => x.DocType).IsUnique().HasDatabaseName("IX_DocumentTemplates_DocType");
        });

        // ── Permission (GLOBAL catalog in public) ───────────────────────────────────
        // Mapped ONLY so the tenant-schema RolePermission/UserPermission FKs can point at it.
        // ExcludeFromMigrations: tenant migrations must NOT create it (it lives in public, owned by
        // the control-plane migrations). EF still emits the cross-schema FK references to it.
        modelBuilder.Entity<Permission>(b =>
        {
            b.ToTable("Permissions", "public", t => t.ExcludeFromMigrations());
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).HasMaxLength(36);
        });

        // ── RolePermission (tenant) → public.Permissions ────────────────────────────
        modelBuilder.Entity<RolePermission>(b =>
        {
            b.ToTable("RolePermissions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(36);
            b.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── UserPermission (tenant) → public.Permissions ────────────────────────────
        modelBuilder.Entity<UserPermission>(b =>
        {
            b.ToTable("UserPermissions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(36);
            b.HasOne(up => up.Permission)
                .WithMany(p => p.UserPermissions)
                .HasForeignKey(up => up.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Global soft-delete query filters
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Role>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Department>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Branch>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<UserRole>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PersonalAccessToken>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PasswordPolicy>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SystemSetting>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SystemModule>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DocumentTemplate>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RolePermission>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<UserPermission>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Permission>().HasQueryFilter(e => !e.IsDeleted);

        // PostgreSQL type mappings (mirror the control-plane context)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetColumnType("TIMESTAMPTZ");
                else if (property.ClrType == typeof(bool))
                    property.SetColumnType("BOOLEAN");
                else if (property.ClrType == typeof(TimeSpan) || property.ClrType == typeof(TimeSpan?))
                    property.SetColumnType("TIME");
            }
        }
    }
}
