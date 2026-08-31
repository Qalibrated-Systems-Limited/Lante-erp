namespace UserService.Core.Entities;

public class UserPermission : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public virtual User User { get; set; } = null!;

    public string PermissionId { get; set; } = string.Empty;
    public virtual Permission Permission { get; set; } = null!;
}
