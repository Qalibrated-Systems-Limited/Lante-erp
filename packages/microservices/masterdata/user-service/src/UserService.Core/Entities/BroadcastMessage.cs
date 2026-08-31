namespace UserService.Core.Entities;

// A record of a platform-admin broadcast — kept so the admin UI can show a history of what
// was sent, when, and how many recipients actually received it (email + in-app notification).
public class BroadcastMessage : BaseEntity
{
    public string Subject        { get; set; } = string.Empty;
    public string Body           { get; set; } = string.Empty;
    public string SentByUserId   { get; set; } = string.Empty;
    public string SentByName     { get; set; } = string.Empty;
    public int    TotalRecipients { get; set; }
    public int    EmailsSent      { get; set; }
    public int    NotificationsSent { get; set; }
    public DateTime SentAt       { get; set; } = DateTime.UtcNow;

    public virtual ICollection<BroadcastRecipient> Recipients { get; set; } = [];
}
