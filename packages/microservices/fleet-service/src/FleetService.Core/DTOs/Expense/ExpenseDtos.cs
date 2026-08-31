using System.ComponentModel.DataAnnotations;

namespace FleetService.Core.DTOs.Expense;

public class CreateExpenseDto
{
    [Required]
    public string TripId { get; set; } = string.Empty;
    [Required]
    public string Description { get; set; } = string.Empty;
    [Required, Range(0, double.MaxValue)]
    public decimal Amount { get; set; }
    public string? Category { get; set; }
}
