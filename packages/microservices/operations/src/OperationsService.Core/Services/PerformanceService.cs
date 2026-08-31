using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Performance;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>
/// #339 -- a technician performance scorecard for a calendar month: assignment throughput and
/// timeliness, field-service-report turnaround, and requisition activity, rolled into a single
/// <see cref="AlertLevel"/> so a manager can scan a department for who needs attention.
///
/// <para><b>Get vs Compute.</b> The <c>Get*</c> methods only read rows <see cref="ComputeMetricsAsync"/>
/// or <see cref="ComputeDepartmentMetricsAsync"/> already wrote -- they do not compute on demand. A
/// technician with no computed row for a period returns null (or is absent from a department
/// summary), same as a report that was never generated. This mirrors the interface's own naming: a
/// verb pair, not one method with an optional side effect.</para>
///
/// <para>Every ratio defaults to 0, not 100, when its denominator is zero (no assignments, no reports
/// submitted). A technician who did nothing this month is not "perfectly on time" -- there is nothing
/// to be on time about, and reading it as a 100% success rate would rank an idle month above a busy
/// one with a single late job.</para>
/// </summary>
public class PerformanceService : IPerformanceService
{
    private readonly IGenericRepository<PerformanceMetrics> _metrics;
    private readonly IGenericRepository<Assignment> _assignments;
    private readonly IGenericRepository<AssignedTechnician> _assignedTechnicians;
    private readonly IGenericRepository<ServiceReport> _serviceReports;
    private readonly IGenericRepository<Requisition> _requisitions;
    private readonly IMapper _mapper;

    public PerformanceService(
        IGenericRepository<PerformanceMetrics> metrics,
        IGenericRepository<Assignment> assignments,
        IGenericRepository<AssignedTechnician> assignedTechnicians,
        IGenericRepository<ServiceReport> serviceReports,
        IGenericRepository<Requisition> requisitions,
        IMapper mapper)
    {
        _metrics = metrics;
        _assignments = assignments;
        _assignedTechnicians = assignedTechnicians;
        _serviceReports = serviceReports;
        _requisitions = requisitions;
        _mapper = mapper;
    }

    public async Task<PerformanceMetricsReadDto?> GetMetricsAsync(string technicianId, int month, int year)
    {
        var (periodStart, periodEnd) = PeriodBounds(month, year);
        var row = await _metrics.Query()
            .FirstOrDefaultAsync(m => m.TechnicianId == technicianId && m.PeriodStart == periodStart);
        if (row == null) return null;

        return await ToReadDtoAsync(row, periodStart, periodEnd);
    }

    public async Task<PerformanceSummaryDto> GetDepartmentSummaryAsync(string departmentId, int month, int year)
    {
        var (periodStart, periodEnd) = PeriodBounds(month, year);

        // Technicians who actually worked in this department during the period, not every technician
        // who ever has -- a department summary for March should not list someone last assigned there
        // in January just because a PerformanceMetrics row of theirs happens to exist.
        var technicianIds = await _assignments.Query()
            .Where(a => a.DepartmentId == departmentId)
            .SelectMany(a => a.Technicians.Select(t => t.UserId))
            .Distinct()
            .ToListAsync();

        var rows = await _metrics.Query()
            .Where(m => m.PeriodStart == periodStart && technicianIds.Contains(m.TechnicianId))
            .ToListAsync();

        var dtos = new List<PerformanceMetricsReadDto>();
        foreach (var row in rows)
            dtos.Add(await ToReadDtoAsync(row, periodStart, periodEnd));

        var totalAssignments = dtos.Sum(d => d.TotalAssignments);
        var completedAssignments = dtos.Sum(d => d.CompletedAssignments);

        return new PerformanceSummaryDto
        {
            DepartmentId = departmentId,
            Month = month,
            Year = year,
            TechnicianMetrics = dtos,
            DepartmentTotalAssignments = totalAssignments,
            DepartmentCompletedAssignments = completedAssignments,
            DepartmentCompletionRate = totalAssignments > 0 ? Round(completedAssignments * 100.0 / totalAssignments) : 0,
        };
    }

    public async Task ComputeMetricsAsync(string technicianId, int month, int year)
    {
        var (periodStart, periodEnd) = PeriodBounds(month, year);

        var assignmentIds = await _assignedTechnicians.Query()
            .Where(t => t.UserId == technicianId && t.AssignedAt >= periodStart && t.AssignedAt <= periodEnd)
            .Select(t => t.AssignmentId)
            .Distinct()
            .ToListAsync();

        var assignments = await _assignments.Query()
            .Where(a => assignmentIds.Contains(a.Id))
            .ToListAsync();

        var totalAssignments = assignments.Count;
        var completed = assignments.Where(a => a.Status == AssignmentStatus.Completed).ToList();
        var delayed = completed.Count(a => a.Deadline != null && a.CompletedAt != null && a.CompletedAt > a.Deadline);

        var completionRate = totalAssignments > 0 ? Round(completed.Count * 100.0 / totalAssignments) : 0;
        var onTimeRate = completed.Count > 0 ? Round((completed.Count - delayed) * 100.0 / completed.Count) : 0;

        var reports = await _serviceReports.Query()
            .Where(r => r.TechnicianId == technicianId
                     && assignmentIds.Contains(r.AssignmentId)
                     && r.SubmittedAt != null && r.SubmittedAt >= periodStart && r.SubmittedAt <= periodEnd)
            .ToListAsync();
        var reportsApproved = reports.Count(r => r.Status == ServiceReportStatus.Approved);
        var reportsRejected = reports.Count(r => r.Status == ServiceReportStatus.Rejected);
        var reportApprovalRate = reports.Count > 0 ? Round(reportsApproved * 100.0 / reports.Count) : 0;

        var minutesLogged = reports.Where(r => r.TotalMinutes.HasValue).Select(r => r.TotalMinutes!.Value).ToList();
        var averageCompletionMinutes = minutesLogged.Count > 0 ? Round(minutesLogged.Average()) : 0;
        var totalWorkingHours = minutesLogged.Count > 0 ? Round(minutesLogged.Sum() / 60.0) : 0;

        var requisitions = await _requisitions.Query()
            .Where(r => r.TechnicianId == technicianId && r.CreatedAt >= periodStart && r.CreatedAt <= periodEnd)
            .ToListAsync();
        var approvedRequisitions = requisitions.Count(IsApproved);

        var performanceScore = Round((completionRate + onTimeRate + reportApprovalRate) / 3.0);

        var row = await _metrics.Query()
            .FirstOrDefaultAsync(m => m.TechnicianId == technicianId && m.PeriodStart == periodStart);
        var isNew = row == null;
        row ??= new PerformanceMetrics { TechnicianId = technicianId, PeriodStart = periodStart };

        row.PeriodEnd = periodEnd;
        row.TotalAssignments = totalAssignments;
        row.CompletedAssignments = completed.Count;
        row.DelayedAssignments = delayed;
        row.CompletionRate = completionRate;
        row.OnTimeRate = onTimeRate;
        row.AverageCompletionMinutes = averageCompletionMinutes;
        row.TotalWorkingHours = totalWorkingHours;
        row.ReportsSubmitted = reports.Count;
        row.ReportsApproved = reportsApproved;
        row.ReportsRejected = reportsRejected;
        row.ReportApprovalRate = reportApprovalRate;
        row.TotalRequisitions = requisitions.Count;
        row.ApprovedRequisitions = approvedRequisitions;
        row.TotalRequisitionAmount = requisitions.Sum(r => r.Amount);
        row.PerformanceScore = performanceScore;
        row.AlertLevel = performanceScore < row.RedThreshold ? AlertLevel.Red
                        : performanceScore < row.AmberThreshold ? AlertLevel.Amber
                        : AlertLevel.None;

        if (isNew) await _metrics.CreateAsync(row);
        else await _metrics.UpdateAsync(row);
    }

    public async Task ComputeDepartmentMetricsAsync(string departmentId, int month, int year)
    {
        var (periodStart, periodEnd) = PeriodBounds(month, year);

        var technicianIds = await _assignments.Query()
            .Where(a => a.DepartmentId == departmentId)
            .SelectMany(a => a.Technicians.Select(t => t.UserId))
            .Distinct()
            .ToListAsync();

        foreach (var technicianId in technicianIds)
            await ComputeMetricsAsync(technicianId, month, year);
    }

    private async Task<PerformanceMetricsReadDto> ToReadDtoAsync(PerformanceMetrics row, DateTime periodStart, DateTime periodEnd)
    {
        var dto = _mapper.Map<PerformanceMetricsReadDto>(row);
        dto.Month = periodStart.Month;
        dto.Year = periodStart.Year;

        // Name/department aren't stored on the row -- resolved from the most recent assignment this
        // technician had inside the period, the same source ComputeMetricsAsync aggregated from.
        var latest = await _assignedTechnicians.Query()
            .Where(t => t.UserId == row.TechnicianId && t.AssignedAt >= periodStart && t.AssignedAt <= periodEnd)
            .OrderByDescending(t => t.AssignedAt)
            .Select(t => new { t.UserName, t.Assignment.DepartmentId })
            .FirstOrDefaultAsync();
        if (latest != null)
        {
            dto.TechnicianName = latest.UserName;
            dto.DepartmentId = latest.DepartmentId;
        }
        return dto;
    }

    private static bool IsApproved(Requisition r) =>
        r.Status is RequisitionStatus.CfoApproved or RequisitionStatus.Paid;

    private static (DateTime Start, DateTime End) PeriodBounds(int month, int year)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1).AddTicks(-1));
    }

    private static double Round(double value) => Math.Round(value, 2);
}
