using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

public class AssetDisposalService : IAssetDisposalService
{
    private const decimal BoardApprovalThreshold = 200_000m;

    private readonly FinanceDbContext _db;
    private readonly IApprovalAuthorityService _authority;
    public AssetDisposalService(FinanceDbContext db, IApprovalAuthorityService authority) { _db = db; _authority = authority; }

    public async Task<List<AssetDisposalReadDto>> ListAsync()
    {
        var disposals = await _db.AssetDisposals.Include(d => d.Asset)
            .OrderByDescending(d => d.DisposalDate).ToListAsync();
        return disposals.Select(ToDto).ToList();
    }

    public async Task<AssetDisposalReadDto> CreateAsync(CreateAssetDisposalDto dto, string? actorUserId)
    {
        var asset = await _db.FixedAssets.FirstOrDefaultAsync(a => a.Id == dto.AssetId)
            ?? throw new InvalidOperationException("Asset not found.");
        if (asset.Status != "Active")
            throw new InvalidOperationException($"Asset is {asset.Status} — only Active assets can be disposed.");

        var nbv = asset.AcquisitionCost - asset.AccumulatedDepreciation;
        var disposal = new AssetDisposal
        {
            AssetId = asset.Id, DisposalDate = dto.DisposalDate, Method = dto.Method, Proceeds = dto.Proceeds,
            ClosingNbv = nbv, RequiresBoardApproval = nbv > BoardApprovalThreshold,
            CreatedBy = actorUserId, UpdatedBy = actorUserId,
        };
        _db.AssetDisposals.Add(disposal);
        await _db.SaveChangesAsync();
        disposal.Asset = asset;
        return ToDto(disposal);
    }

    public async Task<AssetDisposalReadDto> ApproveMdAsync(string id, string? actorUserId, ApprovalContext ctx)
    {
        var disposal = await Load(id);
        if (disposal.Status != "PendingMdApproval")
            throw new InvalidOperationException($"Disposal is {disposal.Status}; expected PendingMdApproval.");

        _authority.Ensure(ctx, "Managing Director");
        disposal.MdApprovedBy = actorUserId;
        disposal.MdApprovedAt = DateTime.UtcNow;

        if (disposal.RequiresBoardApproval)
        {
            disposal.Status = "PendingBoardApproval";
        }
        else
        {
            disposal.Status = "Approved";
            await FinalizeAssetAsync(disposal);
        }
        disposal.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(disposal);
    }

    public async Task<AssetDisposalReadDto> ApproveBoardAsync(string id, string? actorUserId)
    {
        var disposal = await Load(id);
        if (disposal.Status != "PendingBoardApproval")
            throw new InvalidOperationException($"Disposal is {disposal.Status}; expected PendingBoardApproval.");

        disposal.BoardApprovedBy = actorUserId;
        disposal.BoardApprovedAt = DateTime.UtcNow;
        disposal.Status = "Approved";
        disposal.UpdatedAt = DateTime.UtcNow;
        await FinalizeAssetAsync(disposal);
        await _db.SaveChangesAsync();
        return ToDto(disposal);
    }

    private async Task FinalizeAssetAsync(AssetDisposal disposal)
    {
        var asset = disposal.Asset ?? await _db.FixedAssets.FirstOrDefaultAsync(a => a.Id == disposal.AssetId);
        if (asset == null) return;
        asset.Status = "Disposed";
        asset.UpdatedAt = DateTime.UtcNow;
        disposal.Asset = asset;
    }

    private async Task<AssetDisposal> Load(string id) =>
        await _db.AssetDisposals.Include(d => d.Asset).FirstOrDefaultAsync(d => d.Id == id)
        ?? throw new KeyNotFoundException($"Disposal {id} not found.");

    private static AssetDisposalReadDto ToDto(AssetDisposal d) => new()
    {
        Id = d.Id, AssetId = d.AssetId, AssetTag = d.Asset?.AssetTag ?? "", AssetDescription = d.Asset?.Description ?? "",
        DisposalDate = d.DisposalDate, Method = d.Method, Proceeds = d.Proceeds, ClosingNbv = d.ClosingNbv,
        Status = d.Status, MdApprovedBy = d.MdApprovedBy, MdApprovedAt = d.MdApprovedAt,
        RequiresBoardApproval = d.RequiresBoardApproval, BoardApprovedBy = d.BoardApprovedBy, BoardApprovedAt = d.BoardApprovedAt,
    };
}
