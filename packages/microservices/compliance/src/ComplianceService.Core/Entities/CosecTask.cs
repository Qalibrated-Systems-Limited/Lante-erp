using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// STAT-009: Company Secretary workflow — AGM scheduling, annual-return preparation, and
// statutory-register updates, each with a due date and responsible person.
public class CosecTask : BaseEntity
{
    public string? ObligationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ResponsiblePersonUserId { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public DateTime DueDate { get; set; }
    public CosecTaskStatus Status { get; set; } = CosecTaskStatus.Open;
}
