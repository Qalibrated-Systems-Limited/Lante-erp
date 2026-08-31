namespace UserService.Core.Entities;

public class Department : BaseEntity
{
    public string? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    // Departments sharing the same non-null group ID share data visibility (e.g. Technical/Calibration/Construction)
    public string? DepartmentGroupId { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
