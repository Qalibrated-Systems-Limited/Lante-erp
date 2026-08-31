using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>
/// P9 (PROC-002) — SUPPLIER_PERFORMANCE_REVIEW. A biannual, automatically computed score for an approved
/// non-blacklisted supplier, built from transaction data already held in the system: Quality 30 (GRN rejection
/// rate), Delivery 25 (on-time performance), Pricing 25 (invoice accuracy and quote competitiveness),
/// Compliance 20 (document currency, gifts). The overall score feeds back into the ASR — it ranks suppliers in
/// the P3 comparative analysis and, below 40, escalates to the MD who may blacklist via P1.
/// <para>Component scores are <b>nullable</b>: a component with no data in the period is "not assessed" rather
/// than zero, and the overall is rescaled across the weights that were assessed. A supplier the business
/// simply did not trade with must never be scored as a failing one.</para>
/// <para>The input metrics are persisted alongside the scores so any score can be explained and audited
/// rather than appearing as a bare number.</para>
/// </summary>
public class SupplierPerformanceReview : BaseEntity
{
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }

    /// <summary>Biannual period, "YYYY-H1" or "YYYY-H2".</summary>
    public string ReviewPeriod { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // ── Component scores (null = not assessed, no data in the period) ──
    public decimal? QualityScore { get; set; }      // max 30
    public decimal? DeliveryScore { get; set; }     // max 25
    public decimal? PricingScore { get; set; }      // max 25
    public decimal? ComplianceScore { get; set; }   // max 20

    /// <summary>0–100, rescaled across the assessed weights.</summary>
    public decimal OverallScore { get; set; }
    /// <summary>Total weight actually assessed (out of 100) — how much of the score is evidence-backed.</summary>
    public decimal AssessedWeight { get; set; }
    public ReviewOutcome Outcome { get; set; } = ReviewOutcome.NotAssessed;

    // ── Quality inputs ──
    public int PoCount { get; set; }
    public int ReceivedPoCount { get; set; }
    public decimal AcceptedQty { get; set; }
    public decimal RejectedQty { get; set; }
    public decimal RejectRatePct { get; set; }

    // ── Delivery inputs ──
    public int OnTimeCount { get; set; }
    public int LateCount { get; set; }
    public decimal? AvgLeadTimeDays { get; set; }
    /// <summary>True when on-time was measured against promised dates; false when it fell back to lead time
    /// against the target, because no promised dates were recorded.</summary>
    public bool DeliveryFromPromisedDates { get; set; }

    // ── Pricing inputs ──
    public int MatchCount { get; set; }
    public int MatchCleanCount { get; set; }
    public int QuoteCount { get; set; }
    public int LowestQuoteCount { get; set; }

    // ── Compliance inputs ──
    public int CoreDocsRequired { get; set; }
    public int CoreDocsValid { get; set; }
    public int GiftCount { get; set; }
    public bool ConflictFound { get; set; }

    // ── ASR feedback ──
    public decimal? CategoryMinScore { get; set; }
    public bool BelowCategoryThreshold { get; set; }
    /// <summary>Set below 40. A recommendation only — blacklisting stays an MD action in the ASR (P1).</summary>
    public bool RecommendBlacklist { get; set; }
    public string? MdEscalatedBy { get; set; }
    public DateTime? MdEscalatedAt { get; set; }

    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
