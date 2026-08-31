namespace FleetService.Core.Entities;

public class Expense : BaseEntity
{
    public string TripId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReceiptPhotoUrl { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool Synced { get; set; } = false;

    public virtual Trip? Trip { get; set; }
}
