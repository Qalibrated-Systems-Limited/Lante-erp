namespace TicketingService.Core.Entities;

public class TicketAttachment : BaseEntity
{
    public string TicketId { get; set; } = string.Empty;
    public string? TicketCommentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string UploadedByUserId { get; set; } = string.Empty;

    // Navigations
    public Ticket Ticket { get; set; } = null!;
    public TicketComment? TicketComment { get; set; }
}
