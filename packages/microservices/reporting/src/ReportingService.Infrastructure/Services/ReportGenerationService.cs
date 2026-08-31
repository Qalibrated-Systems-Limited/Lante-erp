using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReportingService.Core.Enums;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

public record ReportGenerationResult(bool Success, string? FileUrl, string? ErrorMessage);

/// <summary>
/// Shared render-and-save logic used by both ReportSchedulerBackgroundService (unattended runs,
/// which first mint a system token — see SystemTokenIssuer) and the manual "run now" controller
/// action (which already has the calling user's real HttpContext/JWT, so no impersonation needed).
/// PDF isn't implemented yet — see ReportRenderer.
/// </summary>
public class ReportGenerationService(IConfiguration config)
{
    public async Task<ReportGenerationResult> GenerateAsync(IServiceProvider sp, string schema, string reportDefinitionKey, string runId, ReportFormat format)
    {
        if (format != ReportFormat.Excel)
            return new ReportGenerationResult(false, null, $"{format} rendering isn't implemented yet — only Excel is currently supported.");

        try
        {
            var reportData = await DispatchAsync(sp, reportDefinitionKey);
            var bytes = ReportRenderer.ToExcel(reportData);

            var storageBasePath = config["Storage:BasePath"] ?? "/app/uploads";
            var relativeDir = Path.Combine("reports", schema);
            Directory.CreateDirectory(Path.Combine(storageBasePath, relativeDir));
            var fileName = $"{reportDefinitionKey}-{runId}.xlsx";
            var fullPath = Path.Combine(storageBasePath, relativeDir, fileName);
            await File.WriteAllBytesAsync(fullPath, bytes);

            return new ReportGenerationResult(true, $"/uploads/reports/{schema}/{fileName}", null);
        }
        catch (Exception ex)
        {
            return new ReportGenerationResult(false, null, ex.Message);
        }
    }

    // Maps a ReportDefinition.Key to the matching *ReportService, calling each with the same
    // "no explicit filter" defaults a full/latest run would use. Public so MetricResolverService
    // (DataSource/widget/scorecard/red-flag value resolution) can reuse the same dispatch.
    public static async Task<object> DispatchAsync(IServiceProvider sp, string key) => key switch
    {
        "management-accounts" => await sp.GetRequiredService<IManagementAccountsReportService>().GetAsync(null, null),
        "budget-variance" => await sp.GetRequiredService<IBudgetVarianceReportService>().GetAsync(string.Empty),
        // null, not string.Empty: the service treats a blank fiscal year as "resolve the current one",
        // and both spellings reach the same branch — null is what the controller passes.
        "revenue-vs-target" => await sp.GetRequiredService<IRevenueVsTargetReportService>().GetAsync(null),
        "aged-debtors" => await sp.GetRequiredService<IAgedDebtorsReportService>().GetAsync(null),
        "cash-flow-forecast" => await sp.GetRequiredService<ICashFlowForecastReportService>().GetAsync(null, 13),
        "project-profitability" => await sp.GetRequiredService<IProjectProfitabilityReportService>().GetAsync(null),
        "fleet-cost-utilisation" => await sp.GetRequiredService<IFleetCostUtilisationReportService>().GetAsync(null, null, null),
        "procurement-spend" => await sp.GetRequiredService<IProcurementSpendReportService>().GetAsync(null, null, null, null),
        "hse-incidents-trir" => await sp.GetRequiredService<IHseIncidentsTrirReportService>().GetAsync(null, null, 0m),
        "compliance-dashboard" => await sp.GetRequiredService<IComplianceDashboardReportService>().GetAsync(),
        // Passes sp through: the metric resolver needs a scope to reach the other reports' services.
        "kpi-scorecard-progress" => await sp.GetRequiredService<IKpiScorecardProgressReportService>().GetAsync(sp),
        // null period: the whole schedule. A scheduled run has no period to pass, and defaulting to the
        // current month would make the metric mean something different on the 1st than on the 28th.
        "fixed-asset-register" => await sp.GetRequiredService<IFixedAssetRegisterReportService>().GetAsync(null),
        // null status: the service defaults to Approved, the only figure that was actually paid.
        "payroll-summary" => await sp.GetRequiredService<IPayrollSummaryReportService>().GetAsync(null),
        // null year: the service defaults to the current one.
        "leave-balance" => await sp.GetRequiredService<ILeaveBalanceReportService>().GetAsync(null),
        _ => throw new InvalidOperationException($"Unknown report key '{key}'."),
    };
}
