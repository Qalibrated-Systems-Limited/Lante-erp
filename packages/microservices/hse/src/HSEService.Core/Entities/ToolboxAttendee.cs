namespace HSEService.Core.Entities;

// Junction table resolving the many-to-many between toolbox talks and employees, capturing the
// per-attendee sign-off required by HSE-004 (per the ERD companion design notes).
public class ToolboxAttendee : BaseEntity
{
    public string TalkId { get; set; } = string.Empty;
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;

    public ToolboxTalk? Talk { get; set; }
}
