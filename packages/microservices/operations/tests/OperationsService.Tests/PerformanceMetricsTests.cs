using FluentAssertions;
using OperationsService.Core.Enums;
using Xunit;

namespace OperationsService.Tests;

/// <summary>#339 -- PerformanceService.ComputeMetricsAsync aggregating real Assignment/ServiceReport/
/// Requisition rows into a PerformanceMetrics scorecard, and GetMetricsAsync reading it back.</summary>
public class PerformanceMetricsTests
{
    [Fact]
    public async Task Get_returns_null_when_nothing_has_been_computed_for_the_period()
    {
        using var fx = new PerformanceFixture();

        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        dto.Should().BeNull();
    }

    [Fact]
    public async Task A_completed_on_time_assignment_scores_full_marks()
    {
        using var fx = new PerformanceFixture();
        var a = fx.SeedAssignment(AssignmentStatus.Completed,
            deadline: new DateTime(2026, 3, 20), completedAt: new DateTime(2026, 3, 18));

        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);
        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        dto.Should().NotBeNull();
        dto!.TotalAssignments.Should().Be(1);
        dto.CompletedAssignments.Should().Be(1);
        dto.DelayedAssignments.Should().Be(0);
        dto.CompletionRate.Should().Be(100);
        dto.OnTimeRate.Should().Be(100);
        dto.TechnicianName.Should().Be(PerformanceFixture.TechnicianName);
        dto.DepartmentId.Should().Be(PerformanceFixture.DepartmentId);
    }

    [Fact]
    public async Task An_assignment_completed_after_its_deadline_counts_as_delayed()
    {
        using var fx = new PerformanceFixture();
        fx.SeedAssignment(AssignmentStatus.Completed,
            deadline: new DateTime(2026, 3, 10), completedAt: new DateTime(2026, 3, 15));

        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);
        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        dto!.DelayedAssignments.Should().Be(1);
        dto.OnTimeRate.Should().Be(0);
    }

    [Fact]
    public async Task An_assignment_still_in_progress_is_counted_but_not_completed()
    {
        using var fx = new PerformanceFixture();
        fx.SeedAssignment(AssignmentStatus.InProgress);

        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);
        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        dto!.TotalAssignments.Should().Be(1);
        dto.CompletedAssignments.Should().Be(0);
        dto.CompletionRate.Should().Be(0);
        // No completions this period -- on-time rate has nothing to measure, not a free 100%.
        dto.OnTimeRate.Should().Be(0);
    }

    [Fact]
    public async Task Every_rate_defaults_to_zero_not_a_hundred_when_there_is_nothing_to_measure()
    {
        using var fx = new PerformanceFixture();

        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);
        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        dto.Should().NotBeNull();
        dto!.TotalAssignments.Should().Be(0);
        dto.CompletionRate.Should().Be(0);
        dto.OnTimeRate.Should().Be(0);
        dto.ReportApprovalRate.Should().Be(0);
        dto.PerformanceScore.Should().Be(0);
        dto.AlertLevel.Should().Be(AlertLevel.Red);
    }

    [Fact]
    public async Task Service_report_minutes_average_and_sum_into_hours()
    {
        using var fx = new PerformanceFixture();
        var a = fx.SeedAssignment();
        fx.SeedReport(a.Id, ServiceReportStatus.Approved, totalMinutes: 60, submittedAt: PerformanceFixture.InPeriod);
        fx.SeedReport(a.Id, ServiceReportStatus.Approved, totalMinutes: 120, submittedAt: PerformanceFixture.InPeriod);

        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);
        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        dto!.AverageCompletionMinutes.Should().Be(90);
        dto.TotalWorkingHours.Should().Be(3);
        dto.ReportsSubmitted.Should().Be(2);
        dto.ReportsApproved.Should().Be(2);
        dto.ReportApprovalRate.Should().Be(100);
    }

    [Fact]
    public async Task A_rejected_report_lowers_the_approval_rate_but_still_counts_as_submitted()
    {
        using var fx = new PerformanceFixture();
        var a = fx.SeedAssignment();
        fx.SeedReport(a.Id, ServiceReportStatus.Approved, 60, PerformanceFixture.InPeriod);
        fx.SeedReport(a.Id, ServiceReportStatus.Rejected, 60, PerformanceFixture.InPeriod);

        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);
        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        dto!.ReportsSubmitted.Should().Be(2);
        dto.ReportsApproved.Should().Be(1);
        dto.ReportsRejected.Should().Be(1);
        dto.ReportApprovalRate.Should().Be(50);
    }

    [Fact]
    public async Task An_unsubmitted_draft_report_is_not_counted()
    {
        using var fx = new PerformanceFixture();
        var a = fx.SeedAssignment();
        fx.SeedReport(a.Id, ServiceReportStatus.Draft, totalMinutes: null, submittedAt: null);

        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);
        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        dto!.ReportsSubmitted.Should().Be(0);
    }

    [Fact]
    public async Task Requisitions_are_counted_and_only_fully_approved_ones_count_as_approved()
    {
        using var fx = new PerformanceFixture();
        var a = fx.SeedAssignment();
        fx.SeedRequisition(a.Id, 5000m, RequisitionStatus.CfoApproved);
        fx.SeedRequisition(a.Id, 2000m, RequisitionStatus.TmApproved); // manager sign-off only, not final
        fx.SeedRequisition(a.Id, 1000m, RequisitionStatus.Pending);

        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);
        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        dto!.TotalRequisitions.Should().Be(3);
        dto.ApprovedRequisitions.Should().Be(1);
        dto.TotalRequisitionAmount.Should().Be(8000m);
    }

    [Fact]
    public async Task Recomputing_the_same_period_updates_the_existing_row_rather_than_duplicating_it()
    {
        using var fx = new PerformanceFixture();
        fx.SeedAssignment(AssignmentStatus.Completed, completedAt: new DateTime(2026, 3, 5));
        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        // A second assignment lands after the first compute; recomputing must pick it up in the SAME row.
        fx.SeedAssignment(AssignmentStatus.Completed, completedAt: new DateTime(2026, 3, 20));
        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        fx.Db.PerformanceMetrics.Count(m => m.TechnicianId == PerformanceFixture.TechnicianId).Should().Be(1);
        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);
        dto!.TotalAssignments.Should().Be(2);
    }

    [Fact]
    public async Task An_assignment_assigned_outside_the_period_is_not_counted()
    {
        using var fx = new PerformanceFixture();
        fx.SeedAssignment(AssignmentStatus.Completed, assignedAt: new DateTime(2026, 2, 1));

        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);
        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        dto!.TotalAssignments.Should().Be(0);
    }

    [Fact]
    public async Task Department_summary_only_lists_technicians_who_worked_in_that_department()
    {
        using var fx = new PerformanceFixture();
        fx.SeedAssignment(AssignmentStatus.Completed, completedAt: new DateTime(2026, 3, 5));
        await fx.Performance.ComputeMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);

        var summary = await fx.Performance.GetDepartmentSummaryAsync(PerformanceFixture.DepartmentId, 3, 2026);

        summary.TechnicianMetrics.Should().ContainSingle(m => m.TechnicianId == PerformanceFixture.TechnicianId);
        summary.DepartmentTotalAssignments.Should().Be(1);
        summary.DepartmentCompletedAssignments.Should().Be(1);
        summary.DepartmentCompletionRate.Should().Be(100);
    }

    [Fact]
    public async Task Compute_department_metrics_computes_every_technician_who_worked_there()
    {
        using var fx = new PerformanceFixture();
        fx.SeedAssignment(AssignmentStatus.Completed, completedAt: new DateTime(2026, 3, 5));

        await fx.Performance.ComputeDepartmentMetricsAsync(PerformanceFixture.DepartmentId, 3, 2026);

        var dto = await fx.Performance.GetMetricsAsync(PerformanceFixture.TechnicianId, 3, 2026);
        dto.Should().NotBeNull();
    }
}
