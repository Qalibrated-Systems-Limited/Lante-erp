using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// The dated instance of a StatutoryObligation for one period (e.g. "PAYE — August 2026").
// RAG status (STAT-010: GREEN &gt;60d, AMBER 30-60d, RED &lt;30d/overdue) is deliberately NOT
// stored — it's a pure function of DueDate, computed on read (StatutoryDashboardService), the
// same "derive from date, don't cache a status that can go stale" convention used for
// RegulatoryLicence/HseTrainingRecord elsewhere in this platform.
public class StatutoryDeadline : BaseEntity
{
    public string ObligationId { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public StatutoryDeadlineStatus Status { get; set; } = StatutoryDeadlineStatus.Pending;
    public DateTime? FiledOn { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }

    public StatutoryObligation? Obligation { get; set; }
}
