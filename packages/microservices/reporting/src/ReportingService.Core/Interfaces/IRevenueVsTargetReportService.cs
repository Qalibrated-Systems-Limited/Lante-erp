using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

public interface IRevenueVsTargetReportService
{
    /// Omit fiscalYearId for the current fiscal year, resolved server-side.
    Task<RevenueVsTargetReportDto> GetAsync(string? fiscalYearId);
}
