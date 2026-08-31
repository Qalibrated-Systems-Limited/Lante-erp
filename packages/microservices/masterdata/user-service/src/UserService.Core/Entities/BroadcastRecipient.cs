namespace UserService.Core.Entities;

// One row per admin a given BroadcastMessage was sent to — lets the platform admin see exactly
// who got it, and (via a live per-tenant lookup against ticketing-service, since that's where
// the Notification row actually lives) who has and hasn't read it.
public class BroadcastRecipient : BaseEntity
{
    public string BroadcastMessageId { get; set; } = string.Empty;
    public string UserId             { get; set; } = string.Empty;
    public string UserName           { get; set; } = string.Empty;
    public string Email              { get; set; } = string.Empty;
    public string TenantId           { get; set; } = string.Empty;
    public string TenantName         { get; set; } = string.Empty;
    public string SchemaName         { get; set; } = string.Empty;

    public bool    EmailSent        { get; set; }
    public bool    NotificationSent { get; set; }
    // The ticketing-service Notification.Id — needed to look its IsRead status back up.
    public string? NotificationId   { get; set; }

    public virtual BroadcastMessage? BroadcastMessage { get; set; }
}
