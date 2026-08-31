using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class DailySummary : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string SubmittedByUserId { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int HoursWorked { get; set; }
    public string? Challenges { get; set; }
    public string? NextDayPlan { get; set; }
    public decimal? ExpensesIncurred { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
