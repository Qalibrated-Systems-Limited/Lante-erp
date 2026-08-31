using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

public interface IHseServiceClient
{
    Task<HseDashboardDto?> GetDashboardAsync(decimal hoursWorkedYtd);

    // HSEService's GET /api/v1/hse-incidents takes no query params (no date-range/pagination
    // support server-side today) — returns the full, unfiltered collection every time.
    Task<List<HseIncidentReadDto>?> GetAllIncidentsAsync();
}
