namespace FleetService.Core.Entities;

public class TripDeposit : BaseEntity
{
    public string TripId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? BankName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string MpesaReference { get; set; } = string.Empty;
    public string RawSmsText { get; set; } = string.Empty;
    public string? ScreenshotUrl { get; set; }
    public string UserId { get; set; } = string.Empty;

    public virtual Trip? Trip { get; set; }
}
