namespace TicketingService.Core.Entities;

public class Macro : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public bool IsGlobal { get; set; } = true;

    public TicketCategory? Category { get; set; }
}
