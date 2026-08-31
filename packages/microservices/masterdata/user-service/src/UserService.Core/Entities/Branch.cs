namespace UserService.Core.Entities;

public class Branch : BaseEntity
{
    public string TenantId     { get; set; } = string.Empty;
    public string Name         { get; set; } = string.Empty;
    public string Code         { get; set; } = string.Empty;
    public bool   IsHeadOffice { get; set; } = false;
    public bool   IsActive     { get; set; } = true;

    public virtual Tenant                  Tenant      { get; set; } = null!;
    public virtual ICollection<UserTenant> UserTenants { get; set; } = [];
}
