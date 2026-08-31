using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

/// <summary>Report #11 — Fixed Asset Register &amp; Depreciation Schedule. See #225.</summary>
public interface IFixedAssetRegisterReportService
{
    Task<FixedAssetRegisterReportDto> GetAsync(string? period);
}
