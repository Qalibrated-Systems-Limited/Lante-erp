namespace HSEService.Core.Entities;

// ProjectId is a cross-service reference into Operations' Project (no FK — services own
// separate schemas). ProjectName is denormalized at write-time so lists don't need a
// cross-service call to render, matching Ticket.AssigneeName's convention.
public class Site : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public bool IsActive { get; set; } = true;
}
