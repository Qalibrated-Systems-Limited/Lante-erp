using System.Reflection;
using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;

namespace UserService.Infrastructure.Data;

public class LanteUserServiceDbContext : DbContext
{
    public LanteUserServiceDbContext(DbContextOptions<LanteUserServiceDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;
    public DbSet<UserPermission> UserPermissions { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<PersonalAccessToken> PersonalAccessTokens { get; set; } = null!;
    public DbSet<PasswordPolicy> PasswordPolicies { get; set; } = null!;
    public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
    public DbSet<TenantEmailSettings> TenantEmailSettings { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<SystemModule> SystemModules { get; set; } = null!;
    public DbSet<DocumentTemplate> DocumentTemplates { get; set; } = null!;
    public DbSet<Tenant>               Tenants               { get; set; } = null!;
    public DbSet<Branch>               Branches              { get; set; } = null!;
    public DbSet<UserTenant>           UserTenants           { get; set; } = null!;
    public DbSet<SubscriptionPlan>     SubscriptionPlans     { get; set; } = null!;
    public DbSet<CompanySubscription>  CompanySubscriptions  { get; set; } = null!;
    public DbSet<TenantServiceSchema>  TenantServiceSchemas  { get; set; } = null!;
    public DbSet<BroadcastMessage>     BroadcastMessages     { get; set; } = null!;
    public DbSet<BroadcastRecipient>   BroadcastRecipients   { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Schema-per-tenant (Phase 3 Increment 2): no fixed default schema. Control-plane tables are
        // pinned to public below; tenant-plane tables (Users/Roles/Departments/Branches/UserRoles/
        // UserPermissions/RolePermissions/PersonalAccessTokens/PasswordPolicies) stay UNQUALIFIED so
        // the request search_path (set from the JWT `schema` claim by the connection interceptor)
        // routes them to the caller's tenant schema. No claim (login / platform admin) → public.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Control plane — always public.
        modelBuilder.Entity<Tenant>().ToTable("Tenants", "public");
        // Unique, not just indexed: PlatformController.CreateCompany checks slug uniqueness with a
        // plain AnyAsync before inserting — two concurrent signups for the same slug can both pass
        // that check before either commits. This constraint (not the check) is the real guard;
        // matches the check's IgnoreQueryFilters() semantics, so a deleted tenant's slug/schema also
        // stays unavailable for reuse. SchemaName is a deterministic function of Slug
        // (TenantSlug.ToSchemaName), so it's covered too, but indexed directly as defense-in-depth
        // against any future code path that sets it independently.
        modelBuilder.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();
        modelBuilder.Entity<Tenant>().HasIndex(t => t.SchemaName).IsUnique();
        modelBuilder.Entity<SubscriptionPlan>().ToTable("SubscriptionPlans", "public");
        // Money-column precision backstop (#383): both are monthly/annual list prices.
        modelBuilder.Entity<SubscriptionPlan>().Property(p => p.PriceMonthly).HasColumnType("numeric(18,2)");
        modelBuilder.Entity<SubscriptionPlan>().Property(p => p.PriceAnnual).HasColumnType("numeric(18,2)");
        modelBuilder.Entity<CompanySubscription>().ToTable("CompanySubscriptions", "public");
        modelBuilder.Entity<TenantServiceSchema>().ToTable("TenantServiceSchemas", "public");
        modelBuilder.Entity<Permission>().ToTable("Permissions", "public");
        modelBuilder.Entity<UserTenant>().ToTable("UserTenants", "public");
        modelBuilder.Entity<BroadcastMessage>().ToTable("BroadcastMessages", "public");
        modelBuilder.Entity<BroadcastRecipient>().ToTable("BroadcastRecipients", "public");

        // Global soft-delete query filters
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Role>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Permission>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Department>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<UserRole>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<UserPermission>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RolePermission>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PersonalAccessToken>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PasswordPolicy>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SystemSetting>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SystemModule>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DocumentTemplate>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Tenant>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Branch>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<UserTenant>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SubscriptionPlan>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CompanySubscription>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TenantServiceSchema>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BroadcastMessage>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BroadcastRecipient>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TenantEmailSettings>().HasQueryFilter(e => !e.IsDeleted);

        modelBuilder.Entity<SystemSetting>()
            .HasIndex(s => s.Key)
            .IsUnique();

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.CreatedAt);

        modelBuilder.Entity<SystemModule>()
            .HasIndex(m => m.ModuleKey)
            .IsUnique();

        modelBuilder.Entity<DocumentTemplate>()
            .HasIndex(d => d.DocType)
            .IsUnique();

        // Tenant → Branches (1:M)
        modelBuilder.Entity<Branch>()
            .HasOne(b => b.Tenant)
            .WithMany(t => t.Branches)
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // UserTenant relationships
        modelBuilder.Entity<UserTenant>()
            .HasOne(ut => ut.User)
            .WithMany(u => u.UserTenants)
            .HasForeignKey(ut => ut.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserTenant>()
            .HasOne(ut => ut.Tenant)
            .WithMany(t => t.UserTenants)
            .HasForeignKey(ut => ut.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserTenant>()
            .HasOne(ut => ut.Branch)
            .WithMany(b => b.UserTenants)
            .HasForeignKey(ut => ut.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // BroadcastMessage → Recipients (1:M)
        modelBuilder.Entity<BroadcastRecipient>()
            .HasOne(r => r.BroadcastMessage)
            .WithMany(b => b.Recipients)
            .HasForeignKey(r => r.BroadcastMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tenant → ServiceSchemas (1:M) — control-plane provisioning tracker
        modelBuilder.Entity<TenantServiceSchema>()
            .HasOne(ts => ts.Tenant)
            .WithMany(t => t.ServiceSchemas)
            .HasForeignKey(ts => ts.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TenantServiceSchema>()
            .Property(ts => ts.Status)
            .HasConversion<string>();

        modelBuilder.Entity<TenantServiceSchema>()
            .HasIndex(ts => new { ts.TenantId, ts.ServiceKey })
            .IsUnique();

        // CompanySubscription → Tenant + Plan (1:M each)
        modelBuilder.Entity<CompanySubscription>()
            .HasOne(cs => cs.Tenant)
            .WithMany()
            .HasForeignKey(cs => cs.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CompanySubscription>()
            .HasOne(cs => cs.Plan)
            .WithMany(p => p.Subscriptions)
            .HasForeignKey(cs => cs.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // PostgreSQL type mappings
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
