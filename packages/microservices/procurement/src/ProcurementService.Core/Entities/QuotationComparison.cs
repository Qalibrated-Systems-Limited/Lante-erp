using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>
/// P3 — QUOTATION_COMPARISON. One per PR. Records the sourcing band (required quote count), the recommended
/// supplier and the selection rationale. Completing it (received quotes ≥ required) unblocks P4 LPO
/// generation and stamps the PR's QuotationComparisonId. The recommended quote's scores are denormalised here.
/// </summary>
public class QuotationComparison : BaseEntity
{
    public string PrId { get; set; } = string.Empty;
    public SourcingBand Band { get; set; }
    public int RequiredQuotes { get; set; }

    public string? RecommendedSupplierId { get; set; }
    public string? RecommendedSupplierName { get; set; }
    public string? RecommendedQuotationId { get; set; }
    public string? SelectionReason { get; set; }

    public decimal? PriceScore { get; set; }
    public decimal? QualityScore { get; set; }
    public decimal? DeliveryScore { get; set; }
    public decimal? OverallScore { get; set; }

    public ComparisonStatus Status { get; set; } = ComparisonStatus.Draft;
    public DateTime? CompletedAt { get; set; }
}
