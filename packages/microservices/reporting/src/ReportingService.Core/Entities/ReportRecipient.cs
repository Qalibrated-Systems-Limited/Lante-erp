namespace ReportingService.Core.Entities;

// RPT-004: one email recipient of a ReportSchedule's delivery.
public class ReportRecipient : BaseEntity
{
    public string ReportScheduleId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public bool IsActive { get; set; } = true;
}
