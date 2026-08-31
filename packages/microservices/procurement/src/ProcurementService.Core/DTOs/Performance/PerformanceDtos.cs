namespace ProcurementService.Core.DTOs.Performance;

public class RunReviewDto
{
    /// <summary>Biannual period, "YYYY-H1" or "YYYY-H2". Defaults to the half the current date falls in.</summary>
    public string? Period { get; set; }
}

public class PerformanceReviewDto
{
    public string Id { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string ReviewPeriod { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public decimal? QualityScore { get; set; }
    public decimal? DeliveryScore { get; set; }
    public decimal? PricingScore { get; set; }
    public decimal? ComplianceScore { get; set; }
    public decimal OverallScore { get; set; }
    public decimal AssessedWeight { get; set; }
    public string Outcome { get; set; } = string.Empty;

    // Inputs, so a score can be explained rather than trusted blindly.
    public int PoCount { get; set; }
    public int ReceivedPoCount { get; set; }
    public decimal AcceptedQty { get; set; }
    public decimal RejectedQty { get; set; }
    public decimal RejectRatePct { get; set; }
    public int OnTimeCount { get; set; }
    public int LateCount { get; set; }
    public decimal? AvgLeadTimeDays { get; set; }
    public bool DeliveryFromPromisedDates { get; set; }
    public int MatchCount { get; set; }
    public int MatchCleanCount { get; set; }
    public int QuoteCount { get; set; }
    public int LowestQuoteCount { get; set; }
    public int CoreDocsRequired { get; set; }
    public int CoreDocsValid { get; set; }
    public int GiftCount { get; set; }
    public bool ConflictFound { get; set; }

    public decimal? CategoryMinScore { get; set; }
    public bool BelowCategoryThreshold { get; set; }
    public bool RecommendBlacklist { get; set; }
    public string? MdEscalatedBy { get; set; }
    public DateTime? MdEscalatedAt { get; set; }

    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime ReviewDate { get; set; }
    public string? Notes { get; set; }
}

public class PerformanceRowDto
{
    public string Id { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string ReviewPeriod { get; set; } = string.Empty;
    public decimal? QualityScore { get; set; }
    public decimal? DeliveryScore { get; set; }
    public decimal? PricingScore { get; set; }
    public decimal? ComplianceScore { get; set; }
    public decimal OverallScore { get; set; }
    public decimal AssessedWeight { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public bool BelowCategoryThreshold { get; set; }
    public bool RecommendBlacklist { get; set; }
    public DateTime? MdEscalatedAt { get; set; }
    public DateTime ReviewDate { get; set; }
}

public class PerformanceFilterParams
{
    public string? Period { get; set; }
    public string? SupplierId { get; set; }
    /// <summary>NotAssessed | Satisfactory | Warning | MdEscalation</summary>
    public string? Outcome { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public record PerformanceListResult(List<PerformanceRowDto> Items, int Total);

public class PerformanceSummaryDto
{
    public string? LatestPeriod { get; set; }
    public int Reviewed { get; set; }
    public int Satisfactory { get; set; }
    public int Warning { get; set; }
    public int MdEscalation { get; set; }
    public int NotAssessed { get; set; }
    public int BelowCategoryThreshold { get; set; }
    public int AwaitingMdEscalation { get; set; }
    public decimal? AverageScore { get; set; }
}

public record PerformanceActionResult(string Status, string Message, int Reviewed = 0);

public class EscalateReviewDto
{
    public string? Notes { get; set; }
}
