namespace HSEService.Core.Entities;

// HSE-004: date, site, topic, attendees — signed off by Site Supervisor.
public class ToolboxTalk : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string SupervisorUserId { get; set; } = string.Empty;
    public string? SupervisorName { get; set; }
    public string Topic { get; set; } = string.Empty;
    public DateTime HeldOn { get; set; }

    public ICollection<ToolboxAttendee> Attendees { get; set; } = new List<ToolboxAttendee>();
}
