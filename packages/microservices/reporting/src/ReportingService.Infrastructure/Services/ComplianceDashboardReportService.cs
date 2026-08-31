using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

// Report #9: compliance-dashboard. Combines ComplianceService's compliance-dashboard,
// statutory-dashboard and compliance-policies. There is no total-employee-count anywhere in
// ComplianceService, so AcknowledgedCount is returned per-policy as-is (per spec: don't invent a
// denominator) — the frontend decides how to present it.
public class ComplianceDashboardReportService(
    IComplianceServiceClient compliance,
    ILogger<ComplianceDashboardReportService> logger) : IComplianceDashboardReportService
{
    public async Task<ComplianceDashboardReportDto> GetAsync()
    {
        var report = new ComplianceDashboardReportDto();

        var complianceTask = ReportHelpers.SafeCallAsync(() => compliance.GetComplianceDashboardAsync(), "compliance dashboard", logger);
        var statutoryTask = ReportHelpers.SafeCallAsync(() => compliance.GetStatutoryDashboardAsync(), "statutory dashboard", logger);
        var policiesTask = ReportHelpers.SafeCallAsync(() => compliance.GetPoliciesAsync(), "compliance policies", logger);

        await Task.WhenAll(complianceTask, statutoryTask, policiesTask);

        var (complianceDto, complianceWarning) = complianceTask.Result;
        var (statutoryDto, statutoryWarning) = statutoryTask.Result;
        var (policies, policiesWarning) = policiesTask.Result;

        report.ComplianceDashboard = complianceDto;
        report.StatutoryDashboard = statutoryDto;
        report.Policies = policies ?? new List<PolicyReadDto>();

        foreach (var warning in new[] { complianceWarning, statutoryWarning, policiesWarning })
            if (warning != null) report.Warnings.Add(warning);

        return report;
    }
}
