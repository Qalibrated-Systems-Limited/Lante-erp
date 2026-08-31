using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

/// <summary>Report #10 — every configured KPI scorecard measured against its target. See #225.</summary>
public interface IKpiScorecardProgressReportService
{
    Task<KpiScorecardProgressReportDto> GetAsync(IServiceProvider sp);
}
