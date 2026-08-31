using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Api.Controllers;

// Read-only aggregator: every action calls out to already-implemented microservices (forwarding
// the caller's bearer token) and shapes their responses into report DTOs — this controller itself
// never touches this service's own database. For scheduling/persisting/emailing these same
// reports, see ReportDefinitionsController/ReportSchedulesController/ReportRunsController.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
[Authorize(Policy = "reports.view")]
public class ReportsController(
    IManagementAccountsReportService managementAccounts,
    IBudgetVarianceReportService budgetVariance,
    IRevenueVsTargetReportService revenueVsTarget,
    IAgedDebtorsReportService agedDebtors,
    ICashFlowForecastReportService cashFlowForecast,
    IProjectProfitabilityReportService projectProfitability,
    IFleetCostUtilisationReportService fleetCostUtilisation,
    IProcurementSpendReportService procurementSpend,
    IHseIncidentsTrirReportService hseIncidentsTrir,
    IComplianceDashboardReportService complianceDashboard,
    ILogger<ReportsController> logger,
    IKpiScorecardProgressReportService kpiScorecardProgress,
    IFixedAssetRegisterReportService fixedAssetRegister,
    IPayrollSummaryReportService payrollSummary,
    ILeaveBalanceReportService leaveBalance) : ControllerBase
{
    // 1. GET reports/management-accounts?periodId=&asOf=
    [HttpGet("management-accounts")]
    public async Task<IActionResult> ManagementAccounts([FromQuery] string? periodId, [FromQuery] DateTime? asOf)
    {
        try
        {
            var result = await managementAccounts.GetAsync(periodId, asOf);
            return Ok(new ApiResponse<ManagementAccountsReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build management-accounts report");
            return StatusCode(502, ApiFail("Failed to build the management accounts report — FinanceService is unavailable."));
        }
    }

    // 2. GET reports/budget-variance?fiscalYearId= — omit fiscalYearId for the current fiscal
    // year (BudgetVarianceReportService resolves it server-side).
    [HttpGet("budget-variance")]
    public async Task<IActionResult> BudgetVariance([FromQuery] string? fiscalYearId)
    {
        try
        {
            var result = await budgetVariance.GetAsync(fiscalYearId);
            return Ok(new ApiResponse<BudgetVarianceReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build budget-variance report");
            return StatusCode(502, ApiFail("Failed to build the budget variance report — FinanceService is unavailable."));
        }
    }

    // 3. GET reports/aged-debtors?asOf=
    [HttpGet("aged-debtors")]
    public async Task<IActionResult> AgedDebtors([FromQuery] DateTime? asOf)
    {
        try
        {
            var result = await agedDebtors.GetAsync(asOf);
            return Ok(new ApiResponse<List<DebtorAgingRowDto>> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch aged-debtors report");
            return StatusCode(502, ApiFail("Failed to fetch the aged debtors report — FinanceService is unavailable."));
        }
    }

    // 4. GET reports/cash-flow-forecast?asOf=&weeks= (default 13)
    [HttpGet("cash-flow-forecast")]
    public async Task<IActionResult> CashFlowForecast([FromQuery] DateTime? asOf, [FromQuery] int weeks = 13)
    {
        try
        {
            var result = await cashFlowForecast.GetAsync(asOf, weeks);
            return Ok(new ApiResponse<CashFlowDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch cash-flow-forecast report");
            return StatusCode(502, ApiFail("Failed to fetch the cash flow forecast — FinanceService is unavailable."));
        }
    }

    // 5. GET reports/project-profitability?projectId=
    [HttpGet("project-profitability")]
    public async Task<IActionResult> ProjectProfitability([FromQuery] string? projectId)
    {
        try
        {
            var result = await projectProfitability.GetAsync(projectId);
            return Ok(new ApiResponse<ProjectProfitabilityReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build project-profitability report");
            return StatusCode(502, ApiFail("Failed to build the project profitability report — OperationsService is unavailable."));
        }
    }

    // 6. GET reports/fleet-cost-utilisation?from=&to=&truckId=
    [HttpGet("fleet-cost-utilisation")]
    public async Task<IActionResult> FleetCostUtilisation([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? truckId)
    {
        try
        {
            var result = await fleetCostUtilisation.GetAsync(from, to, truckId);
            return Ok(new ApiResponse<FleetCostUtilisationReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build fleet-cost-utilisation report");
            return StatusCode(502, ApiFail("Failed to build the fleet cost & utilisation report — FleetService is unavailable."));
        }
    }

    // 7. GET reports/procurement-spend?from=&to=&supplierId=&category=
    [HttpGet("procurement-spend")]
    public async Task<IActionResult> ProcurementSpend([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? supplierId, [FromQuery] string? category)
    {
        try
        {
            var result = await procurementSpend.GetAsync(from, to, supplierId, category);
            return Ok(new ApiResponse<ProcurementSpendReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build procurement-spend report");
            return StatusCode(502, ApiFail("Failed to build the procurement spend report — StoreService is unavailable."));
        }
    }

    // 8. GET reports/hse-incidents-trir?from=&to=&hoursWorkedYtd=
    [HttpGet("hse-incidents-trir")]
    public async Task<IActionResult> HseIncidentsTrir([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] decimal hoursWorkedYtd = 0)
    {
        try
        {
            var result = await hseIncidentsTrir.GetAsync(from, to, hoursWorkedYtd);
            return Ok(new ApiResponse<HseIncidentsTrirReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build hse-incidents-trir report");
            return StatusCode(502, ApiFail("Failed to build the HSE incidents/TRIR report — HseService is unavailable."));
        }
    }

    // 9. GET reports/compliance-dashboard
    [HttpGet("compliance-dashboard")]
    public async Task<IActionResult> ComplianceDashboard()
    {
        try
        {
            var result = await complianceDashboard.GetAsync();
            return Ok(new ApiResponse<ComplianceDashboardReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build compliance-dashboard report");
            return StatusCode(502, ApiFail("Failed to build the compliance dashboard report — ComplianceService is unavailable."));
        }
    }

    // 10. GET reports/kpi-scorecard-progress
    [HttpGet("kpi-scorecard-progress")]
    public async Task<IActionResult> KpiScorecardProgress()
    {
        try
        {
            var result = await kpiScorecardProgress.GetAsync(HttpContext.RequestServices);
            return Ok(new ApiResponse<KpiScorecardProgressReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            // 500, not 502. Unlike the nine client-backed reports there is no upstream service to blame:
            // scorecards, data sources and the resolver are all local, and a per-metric failure is already
            // absorbed into the report's Warnings. Reaching here means reporting itself is broken.
            logger.LogError(ex, "Failed to build kpi-scorecard-progress report");
            return StatusCode(500, ApiFail("Failed to build the KPI scorecard progress report.", 500));
        }
    }

    // 11. GET reports/fixed-asset-register?period=
    [HttpGet("fixed-asset-register")]
    public async Task<IActionResult> FixedAssetRegister([FromQuery] string? period = null)
    {
        try
        {
            var result = await fixedAssetRegister.GetAsync(period);
            return Ok(new ApiResponse<FixedAssetRegisterReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build fixed-asset-register report");
            return StatusCode(502, ApiFail("Failed to build the fixed asset register — FinanceService is unavailable."));
        }
    }

    // 12. GET reports/payroll-summary?status= — omit status for Approved runs only, which is the
    // figure that was actually paid rather than everything anyone has ever drafted.
    [HttpGet("payroll-summary")]
    public async Task<IActionResult> PayrollSummary([FromQuery] string? status)
    {
        try
        {
            var result = await payrollSummary.GetAsync(status);
            return Ok(new ApiResponse<PayrollSummaryReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build payroll-summary report");
            return StatusCode(502, ApiFail("Failed to build the payroll summary report — HrService is unavailable."));
        }
    }

    // 13. GET reports/leave-balance?year= — omit year for the current one.
    [HttpGet("leave-balance")]
    public async Task<IActionResult> LeaveBalance([FromQuery] int? year)
    {
        try
        {
            var result = await leaveBalance.GetAsync(year);
            return Ok(new ApiResponse<LeaveBalanceReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build leave-balance report");
            return StatusCode(502, ApiFail("Failed to build the leave balance report — HrService is unavailable."));
        }
    }

    // 14. GET reports/revenue-vs-target?fiscalYearId= — omit for the current fiscal year.
    [HttpGet("revenue-vs-target")]
    public async Task<IActionResult> RevenueVsTarget([FromQuery] string? fiscalYearId)
    {
        try
        {
            var result = await revenueVsTarget.GetAsync(fiscalYearId);
            return Ok(new ApiResponse<RevenueVsTargetReportDto> { Success = true, Data = result, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build revenue-vs-target report");
            return StatusCode(502, ApiFail("Failed to build the revenue vs target report — FinanceService is unavailable."));
        }
    }

    private static ApiResponse<object> ApiFail(string message, int statusCode = 502) =>
        new() { Success = false, Message = message, StatusCode = statusCode };
}
