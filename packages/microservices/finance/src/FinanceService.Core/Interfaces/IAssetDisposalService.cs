using FinanceService.Core.DTOs;

namespace FinanceService.Core.Interfaces;

public interface IAssetDisposalService
{
    Task<List<AssetDisposalReadDto>> ListAsync();
    Task<AssetDisposalReadDto> CreateAsync(CreateAssetDisposalDto dto, string? actorUserId);
    /// Requires caller rank >= Managing Director (ApprovalAuthorityService).
    Task<AssetDisposalReadDto> ApproveMdAsync(string id, string? actorUserId, ApprovalContext ctx);
    /// Records that the Board signed off offline (QSL has no Board user accounts in-system). Only
    /// needed when RequiresBoardApproval is true; finalises disposal and sets FixedAsset.Status=Disposed.
    Task<AssetDisposalReadDto> ApproveBoardAsync(string id, string? actorUserId);
}
