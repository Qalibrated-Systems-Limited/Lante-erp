using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

public class FixedAssetService : IFixedAssetService
{
    private const decimal CapitalizationThreshold = 10_000m;

    // QSL_ERP_Requirements_Specification.pdf §5.1 — all straight-line.
    private static readonly (string Name, decimal Rate, string Life)[] SeedCategories =
    {
        ("IT Equipment & Computers", 0.3333m, "3 years"),
        ("Motor Vehicles",           0.20m,   "5 years"),
        ("Plant & Machinery",        0.15m,   "6-7 years"),
        ("Furniture & Fittings",     0.125m,  "8 years"),
        ("Office Equipment",         0.20m,   "5 years"),
        ("Calibration Equipment",    0.15m,   "6-7 years"),
        ("Leasehold Improvements",   0m,      "Lease term"),
    };

    private readonly FinanceDbContext _db;
    public FixedAssetService(FinanceDbContext db) => _db = db;

    private async Task EnsureCategoriesSeededAsync()
    {
        if (await _db.AssetCategories.AnyAsync()) return;
        var now = DateTime.UtcNow;
        _db.AssetCategories.AddRange(SeedCategories.Select(c => new AssetCategory
        {
            Name = c.Name, AnnualRate = c.Rate, UsefulLifeLabel = c.Life,
            CreatedAt = now, UpdatedAt = now, CreatedBy = "system",
        }));
        await _db.SaveChangesAsync();
    }

    public async Task<List<AssetCategoryReadDto>> ListCategoriesAsync()
    {
        await EnsureCategoriesSeededAsync();
        return await _db.AssetCategories
            .OrderBy(c => c.Name)
            .Select(c => new AssetCategoryReadDto { Id = c.Id, Name = c.Name, AnnualRate = c.AnnualRate, UsefulLifeLabel = c.UsefulLifeLabel })
            .ToListAsync();
    }

    public async Task<List<FixedAssetReadDto>> ListAsync()
    {
        var assets = await _db.FixedAssets.Include(a => a.Category).OrderByDescending(a => a.AcquisitionDate).ToListAsync();
        return assets.Select(ToDto).ToList();
    }

    public async Task<FixedAssetReadDto?> GetAsync(string id)
    {
        var a = await _db.FixedAssets.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        return a == null ? null : ToDto(a);
    }

    public async Task<FixedAssetReadDto> CreateAsync(CreateFixedAssetDto dto, string? actorUserId)
    {
        if (dto.AcquisitionCost < CapitalizationThreshold)
            throw new InvalidOperationException(
                $"Kshs {dto.AcquisitionCost:N2} is below the Kshs {CapitalizationThreshold:N0} capitalisation threshold (ASSET-001) — this must be expensed directly instead of registered as a fixed asset.");

        await EnsureCategoriesSeededAsync();
        var category = await _db.AssetCategories.FirstOrDefaultAsync(c => c.Id == dto.CategoryId)
            ?? throw new InvalidOperationException("Unknown asset category.");

        var asset = new FixedAsset
        {
            CategoryId = category.Id,
            AssetTag = await NextAssetTagAsync(dto.AcquisitionDate.Year),
            Description = dto.Description,
            SerialNumber = dto.SerialNumber,
            AcquisitionCost = dto.AcquisitionCost,
            AcquisitionDate = dto.AcquisitionDate,
            AnnualRateOverride = dto.AnnualRateOverride,
            Location = dto.Location,
            LinkedTruckId = dto.LinkedTruckId,
            CreatedBy = actorUserId, UpdatedBy = actorUserId,
        };
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        asset.Category = category;
        return ToDto(asset);
    }

    private async Task<string> NextAssetTagAsync(int year)
    {
        var count = await _db.FixedAssets.IgnoreQueryFilters().CountAsync(a => a.AcquisitionDate.Year == year);
        return $"FA-{year}-{(count + 1):D4}";
    }

    private static FixedAssetReadDto ToDto(FixedAsset a) => new()
    {
        Id = a.Id, AssetTag = a.AssetTag, CategoryId = a.CategoryId, CategoryName = a.Category?.Name ?? "",
        Description = a.Description, SerialNumber = a.SerialNumber, AcquisitionCost = a.AcquisitionCost,
        AcquisitionDate = a.AcquisitionDate, AnnualRate = a.AnnualRateOverride ?? a.Category?.AnnualRate ?? 0,
        UsefulLifeLabel = a.Category?.UsefulLifeLabel ?? "", AccumulatedDepreciation = a.AccumulatedDepreciation,
        NetBookValue = a.AcquisitionCost - a.AccumulatedDepreciation, Location = a.Location,
        LinkedTruckId = a.LinkedTruckId, Status = a.Status,
    };
}
