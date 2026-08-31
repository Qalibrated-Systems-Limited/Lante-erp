using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;
using ReportingService.Infrastructure.Services;
using Xunit;

namespace ReportingService.Tests;

/// <summary>
/// Report #13 — Leave Balance (#225).
///
/// <para>Untaken leave is an accrued liability, so the arithmetic here is a balance-sheet figure and not
/// an HR convenience. Four of these tests exist because the naive version of each is wrong in a way that
/// reads as plausible: dropping carried-forward days, counting forfeited days, clamping a negative
/// balance to zero, and counting rows instead of employees.</para>
///
/// <para>As with report #12, the stub covers the aggregation and not <c>HrServiceClient</c>'s wire
/// contract with HrService.</para>
/// </summary>
public class LeaveBalanceReportTests
{
    private sealed class StubHr : IHrServiceClient
    {
        public List<LeaveEntitlementRowDto>? Entitlements { get; set; } = new();
        public Exception? Throw { get; set; }

        public int? YearAsked { get; private set; }

        public Task<List<LeaveEntitlementRowDto>?> GetLeaveEntitlementsAsync(int? year)
        {
            YearAsked = year;
            if (Throw != null) throw Throw;
            return Task.FromResult(Entitlements);
        }

        public Task<List<PayrollRunRowDto>?> GetPayrollRunsAsync(string? status) =>
            throw new NotSupportedException("This report does not read payroll.");
    }

    private static LeaveBalanceReportService Sut(StubHr hr) =>
        new(hr, NullLogger<LeaveBalanceReportService>.Instance);

    private static LeaveEntitlementRowDto Row(
        string employeeId,
        string leaveTypeCode = "ANN",
        decimal entitled = 21m,
        decimal taken = 0m,
        decimal carriedForward = 0m,
        decimal forfeited = 0m,
        decimal pending = 0m,
        string? employeeName = null,
        int year = 2026) => new()
        {
            Id = $"{employeeId}-{leaveTypeCode}-{year}",
            EmployeeId = employeeId,
            EmployeeNumber = employeeId,
            EmployeeName = employeeName ?? employeeId,
            LeaveTypeId = leaveTypeCode,
            LeaveTypeCode = leaveTypeCode,
            LeaveTypeName = leaveTypeCode == "ANN" ? "Annual Leave" : "Sick Leave",
            Year = year,
            DaysEntitled = entitled,
            DaysTaken = taken,
            CarriedForwardDays = carriedForward,
            ForfeitedDays = forfeited,
            DaysPending = pending,
        };

    // ── Which year ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Omitting_the_year_reports_the_current_one()
    {
        var hr = new StubHr();

        var report = await Sut(hr).GetAsync(null);

        // Both the request and the echoed value, because a report that fetched 2026 and labelled
        // itself 2025 would be worse than one that failed.
        hr.YearAsked.Should().Be(DateTime.UtcNow.Year);
        report.Year.Should().Be(DateTime.UtcNow.Year);
    }

    [Fact]
    public async Task An_explicit_year_is_passed_through_and_echoed_back()
    {
        var hr = new StubHr();

        var report = await Sut(hr).GetAsync(2024);

        hr.YearAsked.Should().Be(2024);
        report.Year.Should().Be(2024);
    }

    // ── The remaining-days arithmetic ────────────────────────────────────────────

    [Fact]
    public async Task Remaining_days_add_carried_forward_and_subtract_taken_and_forfeited()
    {
        var hr = new StubHr
        {
            Entitlements = new() { Row("e1", entitled: 21m, carriedForward: 5m, taken: 8m, forfeited: 2m) },
        };

        var report = await Sut(hr).GetAsync(2026);

        // 21 + 5 - 8 - 2. Each term is wrong in a distinguishable way if dropped: omitting carried
        // forward gives 11, counting forfeited as available gives 18, ignoring taken gives 24.
        report.Balances.Should().ContainSingle().Which.DaysRemaining.Should().Be(16m);
        report.Totals.DaysRemaining.Should().Be(16m);
    }

    [Fact]
    public async Task Carried_forward_days_alone_can_leave_a_balance_after_full_entitlement_is_taken()
    {
        var hr = new StubHr
        {
            Entitlements = new() { Row("e1", entitled: 21m, carriedForward: 4m, taken: 21m) },
        };

        var report = await Sut(hr).GetAsync(2026);

        // Not overdrawn, and not zero: the four carried days are still the employee's to take, and
        // an implementation that ignored them would both understate the liability and wrongly flag
        // this employee.
        var row = report.Balances.Single();
        row.DaysRemaining.Should().Be(4m);
        row.IsOverdrawn.Should().BeFalse();
    }

    [Fact]
    public async Task An_overdrawn_balance_is_flagged_and_left_negative_rather_than_clamped()
    {
        var hr = new StubHr
        {
            Entitlements = new() { Row("e1", entitled: 21m, taken: 25m) },
        };

        var report = await Sut(hr).GetAsync(2026);

        var row = report.Balances.Single();
        row.IsOverdrawn.Should().BeTrue();
        // Clamping to zero would tidy the total while hiding either an approval that should not have
        // happened or a data problem — and the total is what finance accrues against.
        row.DaysRemaining.Should().Be(-4m);
        report.Totals.DaysRemaining.Should().Be(-4m);
        report.Totals.OverdrawnEmployees.Should().Be(1);
    }

    [Fact]
    public async Task Exactly_exhausting_the_entitlement_is_not_overdrawn()
    {
        var hr = new StubHr
        {
            Entitlements = new() { Row("e1", entitled: 21m, taken: 21m) },
        };

        var report = await Sut(hr).GetAsync(2026);

        // The boundary. `remaining <= 0` would flag every employee who used their full allowance.
        var row = report.Balances.Single();
        row.DaysRemaining.Should().Be(0m);
        row.IsOverdrawn.Should().BeFalse();
        report.Totals.OverdrawnEmployees.Should().Be(0);
    }

    // ── Counting employees, not rows ─────────────────────────────────────────────

    [Fact]
    public async Task Headcount_counts_employees_once_even_with_several_leave_types_each()
    {
        var hr = new StubHr
        {
            Entitlements = new()
            {
                Row("e1", "ANN"), Row("e1", "SICK", entitled: 7m),
                Row("e2", "ANN"), Row("e2", "SICK", entitled: 7m),
            },
        };

        var report = await Sut(hr).GetAsync(2026);

        report.Balances.Should().HaveCount(4);
        // Counting rows would report a workforce of four — headcount multiplied by the number of
        // leave types configured, which grows every time HR adds a type.
        report.Totals.EmployeeCount.Should().Be(2);
    }

    [Fact]
    public async Task An_employee_overdrawn_on_two_leave_types_is_counted_once()
    {
        var hr = new StubHr
        {
            Entitlements = new()
            {
                Row("e1", "ANN", entitled: 21m, taken: 25m),
                Row("e1", "SICK", entitled: 7m, taken: 9m),
            },
        };

        var report = await Sut(hr).GetAsync(2026);

        report.Totals.OverdrawnEmployees.Should().Be(1);
    }

    // ── Per-type breakdown ───────────────────────────────────────────────────────

    [Fact]
    public async Task The_breakdown_groups_by_leave_type_and_counts_distinct_employees_within_each()
    {
        var hr = new StubHr
        {
            Entitlements = new()
            {
                Row("e1", "ANN", entitled: 21m, taken: 5m),
                Row("e2", "ANN", entitled: 21m, taken: 10m),
                Row("e3", "SICK", entitled: 7m, taken: 1m),
            },
        };

        var report = await Sut(hr).GetAsync(2026);

        report.ByType.Should().HaveCount(2);

        var annual = report.ByType.Single(t => t.LeaveTypeCode == "ANN");
        annual.EmployeeCount.Should().Be(2);
        annual.DaysEntitled.Should().Be(42m);
        annual.DaysTaken.Should().Be(15m);
        annual.DaysRemaining.Should().Be(27m);

        var sick = report.ByType.Single(t => t.LeaveTypeCode == "SICK");
        sick.EmployeeCount.Should().Be(1);
        sick.DaysRemaining.Should().Be(6m);

        // The per-type figures must reconcile to the totals, or the report contradicts itself on
        // the same page.
        report.ByType.Sum(t => t.DaysRemaining).Should().Be(report.Totals.DaysRemaining);
    }

    [Fact]
    public async Task A_renamed_leave_type_stays_one_line_rather_than_splitting_in_two()
    {
        // Same LeaveTypeId, two different snapshot labels — what HrService actually stores after
        // someone renames a leave type, because LeaveTypeCode/Name are denormalised onto the
        // entitlement row at creation and never backfilled.
        var hr = new StubHr
        {
            Entitlements = new()
            {
                new LeaveEntitlementRowDto
                {
                    EmployeeId = "e1", LeaveTypeId = "lt-1", LeaveTypeCode = "ANN",
                    LeaveTypeName = "Annual Leave", Year = 2026, DaysEntitled = 21m,
                },
                new LeaveEntitlementRowDto
                {
                    EmployeeId = "e2", LeaveTypeId = "lt-1", LeaveTypeCode = "ANNUAL",
                    LeaveTypeName = "Annual Leave (Staff)", Year = 2026, DaysEntitled = 21m,
                },
            },
        };

        var report = await Sut(hr).GetAsync(2026);

        // Grouping on the label would report one leave type as two, each with half the headcount —
        // and the split would appear the day of a rename, with nothing in the data looking wrong.
        report.ByType.Should().ContainSingle();
        report.ByType.Single().LeaveTypeId.Should().Be("lt-1");
        report.ByType.Single().EmployeeCount.Should().Be(2);
        report.ByType.Single().DaysEntitled.Should().Be(42m);
    }

    [Fact]
    public async Task Two_leave_types_sharing_a_snapshot_label_are_not_merged()
    {
        // The other direction: a retired code reused on a new type. Distinct ids, identical labels.
        var hr = new StubHr
        {
            Entitlements = new()
            {
                new LeaveEntitlementRowDto
                {
                    EmployeeId = "e1", LeaveTypeId = "lt-old", LeaveTypeCode = "ANN",
                    LeaveTypeName = "Annual Leave", Year = 2026, DaysEntitled = 21m,
                },
                new LeaveEntitlementRowDto
                {
                    EmployeeId = "e1", LeaveTypeId = "lt-new", LeaveTypeCode = "ANN",
                    LeaveTypeName = "Annual Leave", Year = 2026, DaysEntitled = 30m,
                },
            },
        };

        var report = await Sut(hr).GetAsync(2026);

        // Merging these would add a 21-day and a 30-day allowance into a single 51-day line and count
        // one employee as one — arithmetic that looks entirely plausible on the page.
        report.ByType.Should().HaveCount(2);
        report.ByType.Select(t => t.LeaveTypeId).Should().BeEquivalentTo(new[] { "lt-old", "lt-new" });
    }

    [Fact]
    public async Task A_leave_type_with_no_label_at_all_still_gets_its_own_line()
    {
        // Both snapshot columns are nullable, so pre-denormalisation rows carry neither.
        var hr = new StubHr
        {
            Entitlements = new()
            {
                new LeaveEntitlementRowDto { EmployeeId = "e1", LeaveTypeId = "lt-1", Year = 2026, DaysEntitled = 21m },
                new LeaveEntitlementRowDto { EmployeeId = "e1", LeaveTypeId = "lt-2", Year = 2026, DaysEntitled = 7m },
            },
        };

        var report = await Sut(hr).GetAsync(2026);

        report.ByType.Should().HaveCount(2);
        report.ByType.Should().OnlyContain(t => t.LeaveTypeCode == null);
    }

    [Fact]
    public async Task A_duplicated_upstream_row_does_not_inflate_the_per_type_headcount()
    {
        // HrService has a unique index on (EmployeeId, LeaveTypeId, Year), so it should never emit this.
        // The index is in another service's database though, not in this contract — and the whole point
        // of a report is that a reader trusts the number. This pins the seam's behaviour if a duplicate
        // ever does arrive: one employee, counted once, rather than a workforce that quietly doubled.
        var hr = new StubHr
        {
            Entitlements = new()
            {
                Row("e1", "ANN", entitled: 21m),
                Row("e1", "ANN", entitled: 21m),
            },
        };

        var report = await Sut(hr).GetAsync(2026);

        report.Balances.Should().HaveCount(2);
        report.ByType.Single().EmployeeCount.Should().Be(1);
        report.Totals.EmployeeCount.Should().Be(1);
    }

    // ── Days already committed ───────────────────────────────────────────────────

    [Fact]
    public async Task Pending_days_reduce_what_is_available_without_reducing_what_is_owed()
    {
        var hr = new StubHr
        {
            Entitlements = new() { Row("e1", entitled: 21m, taken: 3m, pending: 12m) },
        };

        var report = await Sut(hr).GetAsync(2026);

        var row = report.Balances.Single();
        // The liability is unchanged: eighteen days are still untaken and still owed.
        row.DaysRemaining.Should().Be(18m);
        row.DaysPending.Should().Be(12m);
        // But only six are actually bookable. Quoting 18 to this employee would be wrong twice over —
        // they would over-book, and the approver would see a balance that says it was fine.
        row.DaysAvailable.Should().Be(6m);
    }

    [Fact]
    public async Task Pending_days_are_part_of_remaining_and_are_never_added_on_top()
    {
        var hr = new StubHr
        {
            Entitlements = new()
            {
                Row("e1", "ANN", entitled: 21m, taken: 1m, pending: 5m),
                Row("e2", "ANN", entitled: 21m, taken: 0m, pending: 3m),
            },
        };

        var report = await Sut(hr).GetAsync(2026);

        report.Totals.DaysRemaining.Should().Be(41m);
        report.Totals.DaysPending.Should().Be(8m);
        // The invariant a reader needs: pending is a subset, so it can never exceed remaining, and
        // an accrual built from remaining + pending would double-count eight days.
        report.Totals.DaysPending.Should().BeLessThanOrEqualTo(report.Totals.DaysRemaining);
        report.ByType.Single().DaysPending.Should().Be(8m);
    }

    // ── Degraded upstream ────────────────────────────────────────────────────────

    [Fact]
    public async Task An_HR_failure_degrades_to_a_warning_rather_than_a_500()
    {
        var hr = new StubHr { Throw = new HttpRequestException("hr is down") };

        var report = await Sut(hr).GetAsync(2026);

        report.Warnings.Should().ContainSingle().Which.Should().Contain("leave entitlements");
        report.Balances.Should().BeEmpty();
        report.ByType.Should().BeEmpty();
        report.Totals.EmployeeCount.Should().Be(0);
        // Still labelled with the year asked for, so a degraded render is not also mislabelled.
        report.Year.Should().Be(2026);
    }

    [Fact]
    public async Task A_null_body_is_treated_as_no_entitlements_rather_than_throwing()
    {
        var hr = new StubHr { Entitlements = null };

        var report = await Sut(hr).GetAsync(2026);

        report.Balances.Should().BeEmpty();
        report.Warnings.Should().BeEmpty();
    }
}
