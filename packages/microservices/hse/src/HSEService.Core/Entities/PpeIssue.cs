using HSEService.Core.Enums;

namespace HSEService.Core.Entities;

// HSE-003: item issued, to whom, date, condition, return/replacement tracking.
public class PpeIssue : BaseEntity
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string Item { get; set; } = string.Empty;
    public PpeCondition Condition { get; set; } = PpeCondition.New;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReturnedAt { get; set; }
    public DateTime? ReplacementDueAt { get; set; }
}
