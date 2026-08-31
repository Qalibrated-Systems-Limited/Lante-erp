using HrService.Core.DTOs.Org;

namespace HrService.Core.Interfaces.Services;

/// <summary>H1 (HR-DEC-3, HR-005/P32) — job positions and the organisational chart. Departments and branches
/// are read from user-service, which owns them; positions and reporting lines are HR's.</summary>
public interface IOrgService
{
    // Positions
    Task<List<PositionDto>> ListPositionsAsync(bool includeInactive);
    Task<PositionDto?> GetPositionAsync(string id);
    Task<OrgActionResult> CreatePositionAsync(CreatePositionDto dto, string userId);
    Task<OrgActionResult> UpdatePositionAsync(string id, UpdatePositionDto dto, string userId);

    // Org chart (P32)
    Task<OrgChartDto> GetChartAsync(bool includeInactive);
    Task<OrgActionResult> SetReportingLineAsync(string employeeId, SetReportingLineDto dto, string userId);
    /// <summary>Rebuilds every node from the employees' reporting lines — the recovery path when nodes and
    /// employee records have drifted, and how existing employees get nodes.</summary>
    Task<OrgActionResult> RebuildAsync(string userId);

    // Directory reads (user-service owns these)
    Task<List<OrgUnitDto>> ListDepartmentsAsync();
    Task<List<OrgUnitDto>> ListBranchesAsync();
}
