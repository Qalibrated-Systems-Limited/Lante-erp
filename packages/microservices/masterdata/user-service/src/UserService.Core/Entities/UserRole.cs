namespace UserService.Core.Entities;

public class UserRole : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public virtual User User { get; set; } = null!;

    public string RoleId { get; set; } = string.Empty;
    public virtual Role Role { get; set; } = null!;
}
