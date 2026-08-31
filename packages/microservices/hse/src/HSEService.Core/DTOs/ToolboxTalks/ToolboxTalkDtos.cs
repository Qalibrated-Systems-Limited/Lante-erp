namespace HSEService.Core.DTOs.ToolboxTalks;

public class ToolboxAttendeeDto
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
}

public class CreateToolboxTalkDto
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string SupervisorUserId { get; set; } = string.Empty;
    public string? SupervisorName { get; set; }
    public string Topic { get; set; } = string.Empty;
    public DateTime HeldOn { get; set; }
    public List<ToolboxAttendeeDto> Attendees { get; set; } = new();
}

// Core-field edit — Attendees stay fixed after creation (sign-off record, not re-editable here).
public class UpdateToolboxTalkDto
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string SupervisorUserId { get; set; } = string.Empty;
    public string? SupervisorName { get; set; }
    public string Topic { get; set; } = string.Empty;
    public DateTime HeldOn { get; set; }
}

public class ToolboxTalkReadDto
{
    public string Id { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string SupervisorUserId { get; set; } = string.Empty;
    public string? SupervisorName { get; set; }
    public string Topic { get; set; } = string.Empty;
    public DateTime HeldOn { get; set; }
    public List<ToolboxAttendeeDto> Attendees { get; set; } = new();
}
