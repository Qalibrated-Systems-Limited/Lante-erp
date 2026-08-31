namespace ReportingService.Core.DTOs;

/// <summary>
/// Report #13 — Leave Balance Report (#225). Entitlement, taken and remaining per employee, with the
/// accrued liability the balances represent.
/// </summary>
public class LeaveBalanceReportDto
{
    public int Year { get; set; }
    public LeaveBalanceTotalsDto Totals { get; set; } = new();
    public List<LeaveBalanceRowDto> Balances { get; set; } = new();
    public List<LeaveBalanceByTypeDto> ByType { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class LeaveBalanceRowDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string LeaveTypeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? LeaveTypeCode { get; set; }
    public string? LeaveTypeName { get; set; }
    public decimal DaysEntitled { get; set; }
    public decimal CarriedForwardDays { get; set; }
    public decimal DaysTaken { get; set; }
    public decimal ForfeitedDays { get; set; }

    /// <summary>Entitled plus carried forward, less taken and forfeited. Carried-forward days are a real
    /// balance the employee may still take, so omitting them understates the liability; forfeited days
    /// are gone, so counting them would overstate it.</summary>
    public decimal DaysRemaining { get; set; }

    /// <summary>Days already committed to requests still in an approval chain. Part of the liability
    /// like any other untaken day, but not available for a new request.</summary>
    public decimal DaysPending { get; set; }

    /// <summary>What the employee could still book: remaining less pending. Diverges deliberately from
    /// HrService's own <c>DaysAvailable</c>, which is computed off entitled − taken and so ignores
    /// carried-forward and forfeited days. Both are defensible; they are not the same number, and this
    /// note exists so nobody reconciles the two and concludes one is broken.</summary>
    public decimal DaysAvailable { get; set; }

    public bool WasProRated { get; set; }

    /// <summary>Negative remaining days — leave taken beyond entitlement. Flagged rather than clamped to
    /// zero: it is either an approval that should not have happened or a data problem, and clamping hides
    /// both.</summary>
    public bool IsOverdrawn { get; set; }
}

public class LeaveBalanceTotalsDto
{
    public int EmployeeCount { get; set; }
    public decimal DaysEntitled { get; set; }
    public decimal DaysTaken { get; set; }
    public decimal DaysRemaining { get; set; }
    public decimal DaysForfeited { get; set; }

    /// <summary>Untaken days already committed to in-flight requests. Part of DaysRemaining, not on top
    /// of it — these two must never be added together.</summary>
    public decimal DaysPending { get; set; }

    /// <summary>Employees with at least one overdrawn balance.</summary>
    public int OverdrawnEmployees { get; set; }
}

public class LeaveBalanceByTypeDto
{
    /// <summary>The grouping identity. Code and name below are denormalised snapshots on the entitlement
    /// row and are display labels only — see the grouping note in LeaveBalanceReportService.</summary>
    public string LeaveTypeId { get; set; } = string.Empty;

    public string? LeaveTypeCode { get; set; }
    public string? LeaveTypeName { get; set; }
    public int EmployeeCount { get; set; }
    public decimal DaysEntitled { get; set; }
    public decimal DaysTaken { get; set; }
    public decimal DaysRemaining { get; set; }
    public decimal DaysPending { get; set; }
}
