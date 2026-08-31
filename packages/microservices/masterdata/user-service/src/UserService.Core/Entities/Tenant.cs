namespace UserService.Core.Entities;

public class Tenant : BaseEntity
{
    public string Name    { get; set; } = string.Empty;
    // Slug doubles as the tenant subdomain: <slug>.qalibrated.co.ke
    public string Slug    { get; set; } = string.Empty;
    // Postgres schema that holds this tenant's data in every service DB, e.g. "tenant_acme".
    public string SchemaName { get; set; } = string.Empty;
    public bool   IsActive { get; set; } = true;

    public virtual ICollection<Branch>              Branches       { get; set; } = [];
    public virtual ICollection<UserTenant>          UserTenants    { get; set; } = [];
    public virtual ICollection<TenantServiceSchema> ServiceSchemas { get; set; } = [];
}
