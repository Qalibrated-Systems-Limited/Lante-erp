namespace ProcurementService.Core.Entities;

/// <summary>
/// P3 — QUOTATION. A written quote received from an ASR-approved supplier against an approved PR. Scores
/// (price/quality/delivery) are set during comparative analysis; the highest-ranked becomes the LPO source.
/// </summary>
public class Quotation : BaseEntity
{
    public string QuoteNumber { get; set; } = string.Empty;   // QT-{yr}-{seq}
    public string PrId { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public DateTime QuoteDate { get; set; } = DateTime.UtcNow;
    public int ValidityDays { get; set; } = 30;
    public decimal TotalQuoted { get; set; }
    public string Currency { get; set; } = "KES";
    public string? QuotePdfUrl { get; set; }
    public string? Notes { get; set; }

    // Comparative analysis (P3.5) — scored 0–100 each; TotalScore is the weighted blend.
    public decimal? PriceScore { get; set; }
    public decimal? QualityScore { get; set; }
    public decimal? DeliveryScore { get; set; }
    public decimal? TotalScore { get; set; }
    public bool IsRecommended { get; set; }

    public ICollection<QuotationLine> Lines { get; set; } = new List<QuotationLine>();
}
