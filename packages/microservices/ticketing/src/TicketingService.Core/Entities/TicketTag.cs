namespace TicketingService.Core.Entities;

public class TicketTag : BaseEntity
{
    public string TicketId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public string AddedByUserId { get; set; } = string.Empty;

    public Ticket Ticket { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
