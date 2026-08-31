using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

/// <summary>
/// Report #13: leave-balance. Entitlement, taken and remaining per employee (#225).
///
/// <para>Leave balances are an accrued liability, not just an HR courtesy — untaken days are money the
/// business owes. That is why the remaining-days arithmetic is spelled out rather than trusted to a
/// caller, and why overdrawn balances are flagged rather than clamped.</para>
/// </summary>
public class LeaveBalanceReportService(
    IHrServiceClient hr,
    ILogger<LeaveBalanceReportService> logger) : ILeaveBalanceReportService
{
    public async Task<LeaveBalanceReportDto> GetAsync(int? year)
    {
        var effectiveYear = year ?? DateTime.UtcNow.Year;
        var report = new LeaveBalanceReportDto { Year = effectiveYear };

        var (entitlements, warning) = await ReportHelpers.SafeCallAsync(
            () => hr.GetLeaveEntitlementsAsync(effectiveYear), "leave entitlements", logger);
        if (warning != null) report.Warnings.Add(warning);

        report.Balances = (entitlements ?? new List<LeaveEntitlementRowDto>())
            .Select(e =>
            {
                // Carried-forward days are a real balance the employee may still take, so leaving them
                // out understates the liability. Forfeited days are gone, so counting them would
                // overstate it. Both appear on the row so a reader can see the working.
                var remaining = e.DaysEntitled + e.CarriedForwardDays - e.DaysTaken - e.ForfeitedDays;
                return new LeaveBalanceRowDto
                {
                    LeaveTypeId = e.LeaveTypeId,
                    EmployeeId = e.EmployeeId,
                    EmployeeNumber = e.EmployeeNumber,
                    EmployeeName = e.EmployeeName,
                    LeaveTypeCode = e.LeaveTypeCode,
                    LeaveTypeName = e.LeaveTypeName,
                    DaysEntitled = e.DaysEntitled,
                    CarriedForwardDays = e.CarriedForwardDays,
                    DaysTaken = e.DaysTaken,
                    ForfeitedDays = e.ForfeitedDays,
                    DaysRemaining = remaining,
                    DaysPending = e.DaysPending,
                    // Remaining LESS pending. Pending days are still a liability — the employee has not
                    // taken them — but they are already spoken for, so quoting `remaining` to someone
                    // asking "how much leave do I have left to book" overstates it.
                    DaysAvailable = remaining - e.DaysPending,
                    WasProRated = e.WasProRated,
                    // Flagged, never clamped to zero. A negative balance is either an approval that should
                    // not have happened or a data problem, and clamping hides both while making the
                    // liability total look tidy.
                    IsOverdrawn = remaining < 0m,
                };
            })
            .OrderBy(b => b.EmployeeName)
            .ThenBy(b => b.LeaveTypeName)
            .ToList();

        // Grouped on LeaveTypeId, NOT on (code, name).
        //
        // LeaveTypeCode and LeaveTypeName are denormalised nullable columns copied onto the entitlement
        // row when it is created (HrService LeaveService.cs:373) — a snapshot, not a join. Two
        // consequences, and grouping on them gets both wrong: rename a leave type and last year's rows
        // keep the old label, so ONE type splits into two breakdown lines; retire a code and reuse it on
        // a new type and TWO types merge into one line, silently adding their entitlements together.
        // LeaveTypeId is the stable identity. Code and name are then taken from the group for display,
        // using the first row rather than the key because within a group they may legitimately differ.
        report.ByType = report.Balances
            .GroupBy(b => b.LeaveTypeId)
            .Select(g => new LeaveBalanceByTypeDto
            {
                LeaveTypeId = g.Key,
                LeaveTypeCode = g.Select(x => x.LeaveTypeCode).FirstOrDefault(c => c != null),
                LeaveTypeName = g.Select(x => x.LeaveTypeName).FirstOrDefault(n => n != null),
                // HrService has a unique index on (EmployeeId, LeaveTypeId, Year) and this report reads a
                // single year, so an employee should appear at most once per group. Distinct anyway: the
                // constraint lives in another service's database, not in this contract.
                EmployeeCount = g.Select(x => x.EmployeeId).Distinct().Count(),
                DaysEntitled = g.Sum(x => x.DaysEntitled),
                DaysTaken = g.Sum(x => x.DaysTaken),
                DaysRemaining = g.Sum(x => x.DaysRemaining),
                DaysPending = g.Sum(x => x.DaysPending),
            })
            .OrderBy(t => t.LeaveTypeName)
            .ToList();

        report.Totals = new LeaveBalanceTotalsDto
        {
            // DISTINCT employees. One row per employee per leave type, so counting rows would multiply
            // headcount by the number of leave types and report a workforce several times its real size.
            EmployeeCount = report.Balances.Select(b => b.EmployeeId).Distinct().Count(),
            DaysEntitled = report.Balances.Sum(b => b.DaysEntitled),
            DaysTaken = report.Balances.Sum(b => b.DaysTaken),
            DaysRemaining = report.Balances.Sum(b => b.DaysRemaining),
            DaysForfeited = report.Balances.Sum(b => b.ForfeitedDays),
            DaysPending = report.Balances.Sum(b => b.DaysPending),
            OverdrawnEmployees = report.Balances.Where(b => b.IsOverdrawn).Select(b => b.EmployeeId).Distinct().Count(),
        };

        return report;
    }
}
