namespace TicketingService.Core.Entities;

public class Tag : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Description { get; set; }

    public ICollection<TicketTag> TicketTags { get; set; } = new List<TicketTag>();
}
