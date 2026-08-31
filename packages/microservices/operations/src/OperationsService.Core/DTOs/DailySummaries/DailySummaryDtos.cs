using System.ComponentModel.DataAnnotations;

namespace OperationsService.Core.DTOs.DailySummaries;

public class CreateDailySummaryDto
{
    public string AssignmentId { get; set; } = string.Empty;
    [Required]
    public DateTime Date { get; set; }
    [Required]
    public string Summary { get; set; } = string.Empty;
    public int HoursWorked { get; set; }
    public string? Challenges { get; set; }
    public string? NextDayPlan { get; set; }
    public decimal? ExpensesIncurred { get; set; }
}

public class UpdateDailySummaryDto
{
    public string? Summary { get; set; }
    public int? HoursWorked { get; set; }
    public string? Challenges { get; set; }
    public string? NextDayPlan { get; set; }
    public decimal? ExpensesIncurred { get; set; }
}

public class DailySummaryReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string SubmittedByUserId { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int HoursWorked { get; set; }
    public string? Challenges { get; set; }
    public string? NextDayPlan { get; set; }
    public decimal? ExpensesIncurred { get; set; }
    public DateTime CreatedAt { get; set; }
}
