namespace ReportingService.Core.DTOs;

// Mirrors the HrService shapes returned by GET /api/v1/hr/payroll/runs and /api/v1/hr/leave/entitlements.
// Mirrored rather than shared for the same reason as the finance DTOs: reporting must not take a project
// reference on hr, and a mirrored DTO that drifts fails loudly at deserialisation rather than silently.
//
// Deliberately a SUBSET. HrService's PayrollRunDto carries thirty fields including the whole payslip
// collection; a report over runs needs the totals and the period, and pulling every payslip for every
// run would move megabytes to compute a handful of sums.

public class PayrollRunRowDto
{
    public string Id { get; set; } = string.Empty;
    public string RunNumber { get; set; } = string.Empty;
    public string PayrollPeriodCode { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "KES";
    public int EmployeeCount { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalTaxable { get; set; }
    public decimal TotalPaye { get; set; }
    public decimal TotalStatutory { get; set; }
    public decimal TotalOtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalEmployerCost { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? JournalEntryNo { get; set; }
    public DateTime? JournalPostedAt { get; set; }
    public string? JournalError { get; set; }
}

public class LeaveEntitlementRowDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string LeaveTypeId { get; set; } = string.Empty;
    public string? LeaveTypeCode { get; set; }
    public string? LeaveTypeName { get; set; }
    public int Year { get; set; }
    public decimal DaysEntitled { get; set; }
    public decimal DaysTaken { get; set; }
    public decimal CarriedForwardDays { get; set; }
    public decimal ForfeitedDays { get; set; }
    public bool WasProRated { get; set; }

    /// <summary>Days locked up by leave requests still in an approval chain. HrService computes this per
    /// row; dropping it would let this report show 21 days remaining for someone who has already booked
    /// 18 of them.</summary>
    public decimal DaysPending { get; set; }
}
