using FinanceService.Core.DTOs;

namespace FinanceService.Core.Interfaces;

public interface IFixedAssetService
{
    Task<List<AssetCategoryReadDto>> ListCategoriesAsync();
    Task<List<FixedAssetReadDto>> ListAsync();
    Task<FixedAssetReadDto?> GetAsync(string id);
    /// Throws InvalidOperationException when AcquisitionCost is below the Kshs 10,000 capitalisation threshold (ASSET-001).
    Task<FixedAssetReadDto> CreateAsync(CreateFixedAssetDto dto, string? actorUserId);
}
