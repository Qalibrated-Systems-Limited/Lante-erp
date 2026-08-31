using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserService.Core.Entities;

public class Role : BaseEntity
{
    public string? TenantId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public bool IsActive { get; set; } = true;

    // Phase 4 RBAC: seeded standard roles are locked (IsSystem=true) — cannot be edited/deleted or
    // have their permissions changed. Admin-created custom roles are IsSystem=false.
    public bool IsSystem { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    [NotMapped]
    public int UserCount => UserRoles?.Count ?? 0;
}
