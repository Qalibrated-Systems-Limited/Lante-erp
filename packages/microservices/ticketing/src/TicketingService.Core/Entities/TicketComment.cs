namespace TicketingService.Core.Entities;

public class TicketComment : BaseEntity
{
    public string TicketId { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; } = false;

    // Navigations
    public Ticket Ticket { get; set; } = null!;
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
}
