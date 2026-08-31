namespace HSEService.Core.Entities;

// HSE-005: first aid, fire safety, working at heights — certificates with expiry dates and
// renewal alerts (see HseAlertsBackgroundService).
public class HseTrainingRecord : BaseEntity
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string Course { get; set; } = string.Empty;
    public DateTime CompletedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? CertificateUrl { get; set; }
}
