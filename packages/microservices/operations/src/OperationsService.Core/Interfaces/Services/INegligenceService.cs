using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Negligence;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>O9 — negligence incidents: 24h logging window, 5-day response, repeat-offense final
/// warning, and payroll-deduction responses (HR seam).</summary>
public interface INegligenceService
{
    Task<NegligenceIncidentReadDto?> GetByIdAsync(string id);
    Task<PaginatedResult<NegligenceIncidentReadDto>> GetAllAsync(int page, int pageSize, string? employeeId, string? status);
    Task<NegligenceIncidentReadDto> ReportAsync(ReportNegligenceDto dto, string userId);
    Task<NegligenceIncidentReadDto> RespondAsync(string id, RespondNegligenceDto dto, string userId, string userName);
}
