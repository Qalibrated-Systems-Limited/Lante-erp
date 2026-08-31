using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

/// <summary>Report #12 — Payroll Summary &amp; Cost Report. See #225.</summary>
public interface IPayrollSummaryReportService
{
    Task<PayrollSummaryReportDto> GetAsync(string? status);
}
