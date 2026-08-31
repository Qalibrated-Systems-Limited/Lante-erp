using OperationsService.Core.DTOs.Variations;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// O7 — variation orders: Draft → MD approval → client approval. Client approval applies the change
/// (contract value + a budget line + planned budget) and, if billable, raises a Finance invoice.
/// </summary>
public interface IVariationOrderService
{
    Task<VariationOrderReadDto?> GetByIdAsync(string id);
    Task<IEnumerable<VariationOrderReadDto>> GetByProjectAsync(string projectId);
    Task<VariationOrderReadDto> CreateAsync(CreateVariationOrderDto dto, string userId);
    Task<VariationOrderReadDto> UpdateAsync(string id, UpdateVariationOrderDto dto, string userId);
    Task DeleteAsync(string id, string userId);

    Task<VariationOrderReadDto> SubmitForApprovalAsync(string id, string userId);
    Task<VariationOrderReadDto> MdReviewAsync(string id, ReviewVariationOrderDto dto, string userId);
    Task<VariationOrderReadDto> ClientApproveAsync(string id, ClientApproveVariationOrderDto dto, string userId);
}
