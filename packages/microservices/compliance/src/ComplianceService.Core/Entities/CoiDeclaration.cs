using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// COMP-002: annual conflict-of-interest declarations by Department Heads and above.
public class CoiDeclaration : BaseEntity
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int Year { get; set; }
    public DateTime? DeclaredOn { get; set; }
    public bool HasConflict { get; set; }
    public string? Details { get; set; }
    public CoiStatus Status { get; set; } = CoiStatus.Pending;
}
