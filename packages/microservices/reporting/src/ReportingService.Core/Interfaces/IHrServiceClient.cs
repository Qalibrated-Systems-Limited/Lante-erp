using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

/// <summary>
/// HR read seam. The eighth service client, and the one whose absence blocked reports #12 and #13 —
/// reporting could reach finance, operations, fleet, stores, hse, compliance and ticketing but not hr
/// (#225).
/// </summary>
public interface IHrServiceClient
{
    Task<List<PayrollRunRowDto>?> GetPayrollRunsAsync(string? status);
    Task<List<LeaveEntitlementRowDto>?> GetLeaveEntitlementsAsync(int? year);
}
