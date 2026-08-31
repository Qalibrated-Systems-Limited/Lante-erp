using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

// Report #8: hse-incidents-trir. Combines HSEService's dashboard KPIs (TRIR/LTIF/etc, passed
// through as-is) with the incidents list. HSEService's GET /api/v1/hse-incidents has no
// server-side date filtering, so from/to are applied here against Incident.OccurredAt.
public class HseIncidentsTrirReportService(
    IHseServiceClient hse,
    ILogger<HseIncidentsTrirReportService> logger) : IHseIncidentsTrirReportService
{
    public async Task<HseIncidentsTrirReportDto> GetAsync(DateTime? from, DateTime? to, decimal hoursWorkedYtd)
    {
        var report = new HseIncidentsTrirReportDto { From = from, To = to };

        var dashboardTask = ReportHelpers.SafeCallAsync(() => hse.GetDashboardAsync(hoursWorkedYtd), "HSE dashboard KPIs", logger);
        var incidentsTask = ReportHelpers.SafeCallAsync(() => hse.GetAllIncidentsAsync(), "HSE incidents", logger);

        await Task.WhenAll(dashboardTask, incidentsTask);

        var (dashboard, dashboardWarning) = dashboardTask.Result;
        var (incidents, incidentsWarning) = incidentsTask.Result;

        report.Dashboard = dashboard;
        if (dashboardWarning != null) report.Warnings.Add(dashboardWarning);

        var incidentList = incidents ?? new List<HseIncidentReadDto>();
        if (from.HasValue) incidentList = incidentList.Where(i => i.OccurredAt >= from.Value).ToList();
        if (to.HasValue) incidentList = incidentList.Where(i => i.OccurredAt <= to.Value).ToList();
        report.Incidents = incidentList;
        if (incidentsWarning != null) report.Warnings.Add(incidentsWarning);

        return report;
    }
}
