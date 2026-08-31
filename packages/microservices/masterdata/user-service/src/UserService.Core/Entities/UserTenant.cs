namespace UserService.Core.Entities;

public class UserTenant : BaseEntity
{
    public string  UserId    { get; set; } = string.Empty;
    public string  TenantId  { get; set; } = string.Empty;
    public string? BranchId  { get; set; }
    public bool    IsDefault { get; set; } = false;

    public virtual User    User    { get; set; } = null!;
    public virtual Tenant  Tenant  { get; set; } = null!;
    public virtual Branch? Branch  { get; set; }
}
