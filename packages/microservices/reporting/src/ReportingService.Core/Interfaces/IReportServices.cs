using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

public interface IManagementAccountsReportService
{
    Task<ManagementAccountsReportDto> GetAsync(string? periodId, DateTime? asOf);
}

public interface IBudgetVarianceReportService
{
    Task<BudgetVarianceReportDto> GetAsync(string? fiscalYearId);
}

public interface IAgedDebtorsReportService
{
    Task<List<DebtorAgingRowDto>> GetAsync(DateTime? asOf);
}

public interface ICashFlowForecastReportService
{
    Task<CashFlowDto> GetAsync(DateTime? asOf, int weeks);
}

public interface IProjectProfitabilityReportService
{
    Task<ProjectProfitabilityReportDto> GetAsync(string? projectId);
}

public interface IFleetCostUtilisationReportService
{
    Task<FleetCostUtilisationReportDto> GetAsync(DateTime? from, DateTime? to, string? truckId);
}

public interface IProcurementSpendReportService
{
    Task<ProcurementSpendReportDto> GetAsync(DateTime? from, DateTime? to, string? supplierId, string? category);
}

public interface IHseIncidentsTrirReportService
{
    Task<HseIncidentsTrirReportDto> GetAsync(DateTime? from, DateTime? to, decimal hoursWorkedYtd);
}

public interface IComplianceDashboardReportService
{
    Task<ComplianceDashboardReportDto> GetAsync();
}
