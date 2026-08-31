namespace TicketingService.Core.Entities;

public class TicketSatisfactionRating : BaseEntity
{
    public string TicketId { get; set; } = string.Empty;
    public int Rating { get; set; } // 1–5
    public string? Comment { get; set; }
    public string SubmittedByUserId { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
}
