using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

/// <summary>Report #13 — Leave Balance Report. See #225.</summary>
public interface ILeaveBalanceReportService
{
    Task<LeaveBalanceReportDto> GetAsync(int? year);
}
