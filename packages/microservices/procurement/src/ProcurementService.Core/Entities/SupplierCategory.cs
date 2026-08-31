namespace ProcurementService.Core.Entities;

/// <summary>P1 — SUPPLIER_CATEGORY. Classifies suppliers and sets the minimum performance score a supplier
/// in this category must hold to stay approved (feeds P9 review thresholds).</summary>
public class SupplierCategory : BaseEntity
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal MinScoreThreshold { get; set; } = 60m;   // 0–100
    public string? Description { get; set; }
}
