using HSEService.Core.DTOs.Dashboard;

namespace HSEService.Core.Interfaces.Services;

public interface IHseDashboardService
{
    /// <summary>
    /// HSE-008. <paramref name="hoursWorkedYtd"/> drives TRIR/LTIF (incidents per 200k/1M hours
    /// worked) — HSE doesn't own timesheet data, so the caller supplies it (from Fleet/Operations/HR
    /// once those feed hours; 0 or omitted yields Trir/Ltif = 0 rather than a divide-by-zero).
    /// </summary>
    Task<HseDashboardDto> ComputeAsync(decimal hoursWorkedYtd);
}
