using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// STAT-002: status of all annual returns with the Registrar of Companies (2017-2025 backlog and
// ongoing) — filed/pending/overdue; alert to MD and Company Secretary 60 days before each deadline.
public class AnnualReturn : BaseEntity
{
    public int Year { get; set; }
    public DateTime DueDate { get; set; }
    public AnnualReturnStatus Status { get; set; } = AnnualReturnStatus.Pending;
    public DateTime? FiledDate { get; set; }
}
