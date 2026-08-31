namespace ComplianceService.Core.Entities;

public class PolicyAck : BaseEntity
{
    public string PolicyId { get; set; } = string.Empty;
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;

    public Policy? Policy { get; set; }
}
