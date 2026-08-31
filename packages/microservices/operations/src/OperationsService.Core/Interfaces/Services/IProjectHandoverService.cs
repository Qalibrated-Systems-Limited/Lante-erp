using OperationsService.Core.DTOs.Handovers;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>O9 — 8-step project handover with four mandatory e-signatures; permanent once completed.</summary>
public interface IProjectHandoverService
{
    Task<HandoverReadDto?> GetByIdAsync(string id);
    Task<IEnumerable<HandoverReadDto>> GetByProjectAsync(string projectId);
    Task<HandoverReadDto> CreateAsync(CreateHandoverDto dto, string userId);
    Task<HandoverReadDto> UpdateAsync(string id, UpdateHandoverDto dto, string userId);
    Task<HandoverReadDto> AddSignatureAsync(string id, AddHandoverSignatureDto dto, string userId);
    Task<HandoverReadDto> CompleteAsync(string id, string userId);
    Task DeleteAsync(string id, string userId);
}
