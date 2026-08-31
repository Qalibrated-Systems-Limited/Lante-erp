using OperationsService.Core.DTOs.Calibration;
using OperationsService.Core.DTOs.Common;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>O6 — CRUD + queries for the reference-standard register (traceability + expiry).</summary>
public interface IReferenceStandardService
{
    Task<ReferenceStandardReadDto?> GetByIdAsync(string id);
    Task<PaginatedResult<ReferenceStandardReadDto>> GetAllAsync(int page, int pageSize, bool activeOnly);
    Task<ReferenceStandardReadDto> CreateAsync(CreateReferenceStandardDto dto, string userId);
    Task<ReferenceStandardReadDto> UpdateAsync(string id, UpdateReferenceStandardDto dto, string userId);
    Task DeleteAsync(string id, string userId);
}
