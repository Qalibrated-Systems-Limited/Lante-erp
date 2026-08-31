using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P11 — BRAND_ASSET. Central library of logos, templates &amp; brochures accessible to all staff.</summary>
public class BrandAsset : BaseEntity
{
    public string AssetName { get; set; } = string.Empty;
    public BrandAssetType AssetType { get; set; } = BrandAssetType.Other;
    public string? FileUrl { get; set; }
    public string Version { get; set; } = "1.0";
    public string UploadedBy { get; set; } = string.Empty;
}
