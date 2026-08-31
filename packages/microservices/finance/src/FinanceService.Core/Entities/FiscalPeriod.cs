using FinanceService.Core.Enums;

namespace FinanceService.Core.Entities;

/// Process 2 — Fiscal year (1 Jan – 31 Dec per FIN-003). Auto-generates 12 monthly periods.
public class FiscalYear : BaseEntity
{
    public string Name { get; set; } = string.Empty;          // e.g. "FY2026"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public FiscalYearStatus Status { get; set; } = FiscalYearStatus.Open;
    public string? LockedBy { get; set; }
    public DateTime? LockedAt { get; set; }

    public ICollection<AccountingPeriod> Periods { get; set; } = new List<AccountingPeriod>();
}

/// One of the 12 monthly accounting periods. Closed/locked periods reject backdated journals (FIN-004).
public class AccountingPeriod : BaseEntity
{
    public string FiscalYearId { get; set; } = string.Empty;
    public int PeriodNo { get; set; }                          // 1–12
    public string Name { get; set; } = string.Empty;          // e.g. "2026-06"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public PeriodStatus Status { get; set; } = PeriodStatus.Open;
    public string? LockedBy { get; set; }
    public DateTime? LockedAt { get; set; }

    public FiscalYear? FiscalYear { get; set; }
}

/// Process 5 — a month-end close checklist item that must be complete before a period locks.
public class PeriodCloseChecklistItem : BaseEntity
{
    public string PeriodId { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// Audit of every period close / reopen event.
public class PeriodCloseLog : BaseEntity
{
    public string PeriodId { get; set; } = string.Empty;
    public string? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ReopenedBy { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? Notes { get; set; }
}
