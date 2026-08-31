namespace ProcurementService.Core.DTOs.Quotations;

// ── Sourcing band (derived from the PR total) ──
public class SourcingInfoDto
{
    public string PrId { get; set; } = string.Empty;
    public decimal PrTotal { get; set; }
    public string Band { get; set; } = string.Empty;
    public int RequiredQuotes { get; set; }
    public bool MdApprovalRequired { get; set; }
}

// ── Record a quote ──
public class RecordQuotationLineDto
{
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class RecordQuotationDto
{
    public string SupplierId { get; set; } = string.Empty;
    public DateTime? QuoteDate { get; set; }
    public int? ValidityDays { get; set; }
    public string? Currency { get; set; }
    public string? QuotePdfUrl { get; set; }
    public string? Notes { get; set; }
    public decimal? TotalQuoted { get; set; }   // optional override; else summed from lines
    public List<RecordQuotationLineDto> Lines { get; set; } = new();
}

public class QuotationLineDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class QuotationDto
{
    public string Id { get; set; } = string.Empty;
    public string QuoteNumber { get; set; } = string.Empty;
    public string PrId { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public DateTime QuoteDate { get; set; }
    public int ValidityDays { get; set; }
    public decimal TotalQuoted { get; set; }
    public string Currency { get; set; } = "KES";
    public string? QuotePdfUrl { get; set; }
    public string? Notes { get; set; }
    public decimal? PriceScore { get; set; }
    public decimal? QualityScore { get; set; }
    public decimal? DeliveryScore { get; set; }
    public decimal? TotalScore { get; set; }
    public bool IsRecommended { get; set; }
    /// <summary>P9 feedback — the supplier's latest biannual performance score (0–100), so the comparative
    /// analysis can rank on past performance and not price alone. Null until the supplier has been reviewed.</summary>
    public decimal? SupplierOverallScore { get; set; }
    public DateTime? SupplierLastReviewedAt { get; set; }
    public List<QuotationLineDto> Lines { get; set; } = new();
}

// ── Scoring + completion ──
public class ScoreQuotationDto
{
    public decimal PriceScore { get; set; }
    public decimal QualityScore { get; set; }
    public decimal DeliveryScore { get; set; }
}

public class CompleteComparisonDto
{
    public string? RecommendedQuotationId { get; set; }   // required unless DirectLpo band
    public string SelectionReason { get; set; } = string.Empty;
}

public class ComparisonDto
{
    public string PrId { get; set; } = string.Empty;
    public string Band { get; set; } = string.Empty;
    public int RequiredQuotes { get; set; }
    public int ReceivedQuotes { get; set; }
    public bool MdApprovalRequired { get; set; }
    public string Status { get; set; } = "Draft";
    public string? RecommendedSupplierId { get; set; }
    public string? RecommendedSupplierName { get; set; }
    public string? RecommendedQuotationId { get; set; }
    public string? SelectionReason { get; set; }
    public decimal? OverallScore { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<QuotationDto> Quotations { get; set; } = new();
}

public record QuotationActionResult(string Status, string Message);
