using HSEService.Core.Enums;

namespace HSEService.Core.Entities;

public class CorrectiveAction : BaseEntity
{
    public string IncidentId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public CorrectiveActionStatus Status { get; set; } = CorrectiveActionStatus.Open;
    public DateTime DueDate { get; set; }
    public DateTime? ClosedAt { get; set; }

    public HseIncident? Incident { get; set; }
}
