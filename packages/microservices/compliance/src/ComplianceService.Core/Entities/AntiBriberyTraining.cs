namespace ComplianceService.Core.Entities;

// COMP-009: staff completion records; 2-year renewal cycle. NextDueOn is computed at
// creation (CompletedOn + 2 years).
public class AntiBriberyTraining : BaseEntity
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public DateTime CompletedOn { get; set; }
    public DateTime NextDueOn { get; set; }
    public string? CertificateUrl { get; set; }
}
