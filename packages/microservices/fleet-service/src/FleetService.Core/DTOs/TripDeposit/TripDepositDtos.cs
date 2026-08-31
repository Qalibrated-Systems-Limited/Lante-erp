using System.ComponentModel.DataAnnotations;

namespace FleetService.Core.DTOs.TripDeposit;

public class CreateTripDepositDto
{
    [Required]
    public string TripId { get; set; } = string.Empty;
    [Required, Range(0, double.MaxValue)]
    public decimal Amount { get; set; }
    public string? BankName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    [Required]
    public DateTime TransactionDate { get; set; }
    [Required]
    public string MpesaReference { get; set; } = string.Empty;
    [Required]
    public string RawSmsText { get; set; } = string.Empty;
}
