using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.DTOs.Performance;
using ProcurementService.Core.Entities;
using ProcurementService.Core.Enums;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Core.Services;

/// <summary>
/// P9 (PROC-002) — the biannual supplier performance review engine. Scores every approved, non-blacklisted
/// supplier out of 100 from data the system already holds, and writes the result back to the ASR.
/// <para><b>Weights</b> Quality 30 (GRN rejection rate), Delivery 25 (on-time), Pricing 25 (invoice accuracy +
/// quote competitiveness), Compliance 20 (core document currency, gifts, conflict of interest).</para>
/// <para><b>No data is not a bad score.</b> Each component is only scored when there is evidence for it in the
/// period; the overall is then rescaled across the assessed weights and <see cref="SupplierPerformanceReview.AssessedWeight"/>
/// records how much of the score is evidence-backed. A supplier with no transactions at all comes out
/// <see cref="ReviewOutcome.NotAssessed"/> and its ASR score is left untouched — otherwise simply not trading
/// with a supplier for six months would drive it towards blacklisting.</para>
/// <para><b>The review never blacklists.</b> Below 40 it sets <see cref="SupplierPerformanceReview.RecommendBlacklist"/>
/// and escalates to the MD, who decides through the ASR (P1) where a reason is mandatory.</para>
/// </summary>
public class PerformanceReviewService(
    IGenericRepository<SupplierPerformanceReview> reviews,
    IGenericRepository<Supplier> suppliers,
    IGenericRepository<SupplierCategory> categories,
    IGenericRepository<SupplierDocument> documents,
    IGenericRepository<GiftRegister> gifts,
    IGenericRepository<PurchaseOrder> pos,
    IGenericRepository<ThreeWayMatch> matches,
    IGenericRepository<Quotation> quotations,
    IGenericRepository<ProcurementAuditLog> audit,
    IMapper mapper) : IPerformanceReviewService
{
    private const decimal QualityWeight = 30m, DeliveryWeight = 25m, PricingWeight = 25m, ComplianceWeight = 20m;
    private const decimal WarningThreshold = 60m, EscalationThreshold = 40m;
    /// <summary>Target order-to-receipt days, used only when no promised delivery dates were recorded.</summary>
    private const decimal LeadTimeTargetDays = 30m;
    /// <summary>Within 10% of the cheapest quote still counts as competitive.</summary>
    private const decimal CompetitiveTolerance = 0.10m;

    /// <summary>The documents whose currency actually matters for compliance ("Other" and bank details are
    /// not compliance instruments).</summary>
    private static readonly SupplierDocumentType[] CoreDocTypes =
    [
        SupplierDocumentType.CertificateOfIncorporation,
        SupplierDocumentType.KraPinCertificate,
        SupplierDocumentType.TaxCompliance,
    ];

    // ── Reads ──
    public async Task<PerformanceListResult> GetAllAsync(PerformanceFilterParams filter)
    {
        var q = reviews.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Period)) q = q.Where(x => x.ReviewPeriod == filter.Period);
        if (!string.IsNullOrWhiteSpace(filter.SupplierId)) q = q.Where(x => x.SupplierId == filter.SupplierId);
        if (!string.IsNullOrWhiteSpace(filter.Outcome) && Enum.TryParse<ReviewOutcome>(filter.Outcome, true, out var o))
            q = q.Where(x => x.Outcome == o);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.ReviewPeriod).ThenByDescending(x => x.OverallScore)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
        return new PerformanceListResult(mapper.Map<List<PerformanceRowDto>>(items), total);
    }

    public async Task<PerformanceReviewDto?> GetByIdAsync(string id)
    {
        var r = await reviews.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return r is null ? null : mapper.Map<PerformanceReviewDto>(r);
    }

    public async Task<List<PerformanceRowDto>> GetHistoryAsync(string supplierId)
    {
        var list = await reviews.Query().AsNoTracking()
            .Where(x => x.SupplierId == supplierId)
            .OrderByDescending(x => x.ReviewPeriod).ToListAsync();
        return mapper.Map<List<PerformanceRowDto>>(list);
    }

    public async Task<PerformanceSummaryDto> GetSummaryAsync(string? period)
    {
        var effective = string.IsNullOrWhiteSpace(period)
            ? await reviews.Query().AsNoTracking().OrderByDescending(x => x.ReviewPeriod)
                .Select(x => x.ReviewPeriod).FirstOrDefaultAsync()
            : period;
        if (string.IsNullOrWhiteSpace(effective)) return new PerformanceSummaryDto();

        var all = await reviews.Query().AsNoTracking().Where(x => x.ReviewPeriod == effective).ToListAsync();
        var assessed = all.Where(x => x.Outcome != ReviewOutcome.NotAssessed).ToList();
        return new PerformanceSummaryDto
        {
            LatestPeriod = effective,
            Reviewed = all.Count,
            Satisfactory = all.Count(x => x.Outcome == ReviewOutcome.Satisfactory),
            Warning = all.Count(x => x.Outcome == ReviewOutcome.Warning),
            MdEscalation = all.Count(x => x.Outcome == ReviewOutcome.MdEscalation),
            NotAssessed = all.Count(x => x.Outcome == ReviewOutcome.NotAssessed),
            BelowCategoryThreshold = all.Count(x => x.BelowCategoryThreshold),
            AwaitingMdEscalation = all.Count(x => x.Outcome == ReviewOutcome.MdEscalation && x.MdEscalatedAt == null),
            AverageScore = assessed.Count == 0 ? null : Math.Round(assessed.Average(x => x.OverallScore), 2),
        };
    }

    // ── The engine ──
    public async Task<PerformanceActionResult> RunAsync(RunReviewDto dto, string userId)
    {
        var period = string.IsNullOrWhiteSpace(dto.Period) ? CurrentPeriod() : dto.Period;
        if (!TryPeriod(period, out var start, out var end))
            return new PerformanceActionResult("Error", "Give the review period as YYYY-H1 or YYYY-H2.");

        // Only approved, non-blacklisted suppliers are reviewed (PROC-002).
        var eligible = await suppliers.Query()
            .Where(s => s.IsApproved && !s.BlacklistFlag)
            .Select(s => s.Id).ToListAsync();
        if (eligible.Count == 0)
            return new PerformanceActionResult("Ok", "No approved, non-blacklisted suppliers to review.");

        int assessed = 0, notAssessed = 0, warnings = 0, escalations = 0;
        foreach (var supplierId in eligible)
        {
            var review = await ScoreAsync(supplierId, period, start, end, userId);
            if (review is null) continue;
            if (review.Outcome == ReviewOutcome.NotAssessed) notAssessed++; else assessed++;
            if (review.Outcome == ReviewOutcome.Warning) warnings++;
            if (review.Outcome == ReviewOutcome.MdEscalation) escalations++;
        }

        await LogAsync(period, AsrAuditAction.PerformanceReviewRun,
            $"Biannual performance review {period}: {assessed} scored, {notAssessed} with no data, {warnings} warning(s), {escalations} escalated to the MD.", userId);
        return new PerformanceActionResult("Ok",
            $"{period}: {assessed} supplier(s) scored, {notAssessed} with no transaction data, {warnings} warning(s), {escalations} for MD escalation.",
            assessed + notAssessed);
    }

    public async Task<PerformanceActionResult> RunForSupplierAsync(string supplierId, RunReviewDto dto, string userId)
    {
        var period = string.IsNullOrWhiteSpace(dto.Period) ? CurrentPeriod() : dto.Period;
        if (!TryPeriod(period, out var start, out var end))
            return new PerformanceActionResult("Error", "Give the review period as YYYY-H1 or YYYY-H2.");

        var supplier = await suppliers.GetByIdAsync(supplierId);
        if (supplier is null) return new PerformanceActionResult("Error", "Supplier not found.");
        if (!supplier.IsApproved || supplier.BlacklistFlag)
            return new PerformanceActionResult("Error", "Only approved, non-blacklisted suppliers are reviewed.");

        var review = await ScoreAsync(supplierId, period, start, end, userId);
        if (review is null) return new PerformanceActionResult("Error", "Supplier not found.");
        return new PerformanceActionResult("Ok",
            review.Outcome == ReviewOutcome.NotAssessed
                ? $"{supplier.Name}: no transaction data in {period} — not assessed."
                : $"{supplier.Name}: {review.OverallScore:N1}/100 ({review.Outcome}) for {period}.", 1);
    }

    /// <summary>Computes and persists one supplier's review, then writes the score back to the ASR.</summary>
    private async Task<SupplierPerformanceReview?> ScoreAsync(
        string supplierId, string period, DateTime start, DateTime end, string userId)
    {
        var supplier = await suppliers.GetByIdAsync(supplierId);
        if (supplier is null) return null;

        // Orders raised in the window are the unit of assessment.
        var periodPos = await pos.Query().AsNoTracking()
            .Where(p => p.SupplierId == supplierId && p.CreatedAt >= start && p.CreatedAt < end)
            .ToListAsync();
        var poIds = periodPos.Select(p => p.Id).ToList();

        var review = await reviews.Query().FirstOrDefaultAsync(r => r.SupplierId == supplierId && r.ReviewPeriod == period)
                     ?? new SupplierPerformanceReview { SupplierId = supplierId, ReviewPeriod = period, CreatedBy = userId };

        review.SupplierName = supplier.Name;
        review.PeriodStart = start;
        review.PeriodEnd = end;
        review.PoCount = periodPos.Count;

        // ── Quality (30): GRN rejection rate over what was actually delivered ──
        var received = periodPos.Where(p => p.ReceivedQty > 0 || p.RejectedQty > 0).ToList();
        review.ReceivedPoCount = received.Count;
        review.AcceptedQty = received.Sum(p => p.ReceivedQty);
        review.RejectedQty = received.Sum(p => p.RejectedQty);
        var handled = review.AcceptedQty + review.RejectedQty;
        if (handled > 0)
        {
            review.RejectRatePct = Round2(review.RejectedQty / handled * 100m);
            review.QualityScore = Round2(QualityWeight * (1m - review.RejectedQty / handled));
        }
        else
        {
            review.RejectRatePct = 0m;
            review.QualityScore = null;   // nothing delivered in the window
        }

        // ── Delivery (25): on-time against promised dates, else lead time against target ──
        var deliveredWithDates = received.Where(p => p.ReceivedAt != null).ToList();
        var withPromise = deliveredWithDates.Where(p => p.PromisedDeliveryDate != null).ToList();
        // Orders promised within the window and still not delivered are late, not absent.
        var overdueUndelivered = periodPos
            .Where(p => p.PromisedDeliveryDate != null && p.ReceivedAt == null && p.PromisedDeliveryDate < end)
            .ToList();

        if (withPromise.Count + overdueUndelivered.Count > 0)
        {
            review.OnTimeCount = withPromise.Count(p => p.ReceivedAt <= p.PromisedDeliveryDate);
            review.LateCount = withPromise.Count(p => p.ReceivedAt > p.PromisedDeliveryDate) + overdueUndelivered.Count;
            var considered = review.OnTimeCount + review.LateCount;
            review.DeliveryFromPromisedDates = true;
            review.DeliveryScore = considered == 0 ? null : Round2(DeliveryWeight * ((decimal)review.OnTimeCount / considered));
        }
        else if (deliveredWithDates.Count > 0)
        {
            // No promised dates recorded — approximate from order-to-receipt time against the target.
            var avg = (decimal)deliveredWithDates.Average(p => (p.ReceivedAt!.Value - p.CreatedAt).TotalDays);
            review.AvgLeadTimeDays = Round2(avg);
            review.DeliveryFromPromisedDates = false;
            review.OnTimeCount = deliveredWithDates.Count(p => (p.ReceivedAt!.Value - p.CreatedAt).TotalDays <= (double)LeadTimeTargetDays);
            review.LateCount = deliveredWithDates.Count - review.OnTimeCount;
            var ratio = avg <= LeadTimeTargetDays ? 1m : LeadTimeTargetDays / avg;
            review.DeliveryScore = Round2(DeliveryWeight * Math.Clamp(ratio, 0m, 1m));
        }
        else
        {
            review.OnTimeCount = 0; review.LateCount = 0; review.AvgLeadTimeDays = null;
            review.DeliveryFromPromisedDates = false;
            review.DeliveryScore = null;
        }

        // ── Pricing (25): invoice accuracy (15) + quote competitiveness (10) ──
        var periodMatches = poIds.Count == 0
            ? new List<ThreeWayMatch>()
            : await matches.Query().AsNoTracking().Where(m => poIds.Contains(m.PoId)).ToListAsync();
        review.MatchCount = periodMatches.Count;
        review.MatchCleanCount = periodMatches.Count(m => m.PriceOk && m.TotalOk);

        var periodQuotes = await quotations.Query().AsNoTracking()
            .Where(q => q.SupplierId == supplierId && q.CreatedAt >= start && q.CreatedAt < end)
            .ToListAsync();
        review.QuoteCount = periodQuotes.Count;
        review.LowestQuoteCount = 0;
        foreach (var quote in periodQuotes)
        {
            var rivals = await quotations.Query().AsNoTracking()
                .Where(q => q.PrId == quote.PrId).Select(q => q.TotalQuoted).ToListAsync();
            if (rivals.Count == 0) continue;
            var lowest = rivals.Min();
            if (lowest <= 0) continue;
            if (quote.TotalQuoted <= lowest * (1m + CompetitiveTolerance)) review.LowestQuoteCount++;
        }

        const decimal accuracyWeight = 15m, competitivenessWeight = 10m;
        decimal? pricing = null;
        if (review.MatchCount > 0 && review.QuoteCount > 0)
            pricing = accuracyWeight * ((decimal)review.MatchCleanCount / review.MatchCount)
                    + competitivenessWeight * ((decimal)review.LowestQuoteCount / review.QuoteCount);
        else if (review.MatchCount > 0)   // only invoice evidence — scale accuracy across the full weight
            pricing = PricingWeight * ((decimal)review.MatchCleanCount / review.MatchCount);
        else if (review.QuoteCount > 0)   // only quote evidence
            pricing = PricingWeight * ((decimal)review.LowestQuoteCount / review.QuoteCount);
        review.PricingScore = pricing is null ? null : Round2(pricing.Value);

        // ── Compliance (20): core document currency (12) + gifts & conflict of interest (8) ──
        var docs = await documents.Query().AsNoTracking().Where(d => d.SupplierId == supplierId).ToListAsync();
        review.CoreDocsRequired = CoreDocTypes.Length;
        review.CoreDocsValid = CoreDocTypes.Count(t => docs.Any(d =>
            d.DocumentType == t && d.VerifiedAt != null && (d.ExpiryDate == null || d.ExpiryDate > end)));

        review.GiftCount = await gifts.Query().AsNoTracking()
            .CountAsync(g => g.SupplierId == supplierId && g.DeclaredAt >= start && g.DeclaredAt < end);
        review.ConflictFound = supplier.ConflictFound;

        const decimal docWeight = 12m, integrityWeight = 8m;
        var docScore = docWeight * ((decimal)review.CoreDocsValid / review.CoreDocsRequired);
        // Declared gifts are a risk signal, not a breach (an undeclared gift is by nature invisible here);
        // an unresolved conflict of interest is the heavier deduction.
        var integrity = integrityWeight - Math.Min(integrityWeight, review.GiftCount * 2m) - (review.ConflictFound ? 4m : 0m);
        review.ComplianceScore = Round2(docScore + Math.Max(0m, integrity));   // always assessable

        // ── Overall, rescaled across the weights that had evidence ──
        var parts = new List<(decimal? Score, decimal Weight)>
        {
            (review.QualityScore, QualityWeight),
            (review.DeliveryScore, DeliveryWeight),
            (review.PricingScore, PricingWeight),
            (review.ComplianceScore, ComplianceWeight),
        };
        var scored = parts.Where(p => p.Score.HasValue).ToList();
        review.AssessedWeight = scored.Sum(p => p.Weight);
        var hasTransactions = review.PoCount > 0 || review.QuoteCount > 0;

        if (!hasTransactions)
        {
            // Compliance alone would let an untraded supplier drift towards escalation — refuse to judge.
            review.OverallScore = 0m;
            review.AssessedWeight = 0m;
            review.Outcome = ReviewOutcome.NotAssessed;
            review.RecommendBlacklist = false;
            review.Notes = "No purchase orders or quotations in the period — not assessed.";
        }
        else
        {
            var raw = scored.Sum(p => p.Score!.Value);
            review.OverallScore = review.AssessedWeight <= 0 ? 0m : Round2(raw / review.AssessedWeight * 100m);
            review.Outcome = review.OverallScore >= WarningThreshold ? ReviewOutcome.Satisfactory
                           : review.OverallScore >= EscalationThreshold ? ReviewOutcome.Warning
                           : ReviewOutcome.MdEscalation;
            review.RecommendBlacklist = review.Outcome == ReviewOutcome.MdEscalation;
            review.Notes = review.AssessedWeight < 100m
                ? $"Scored on {review.AssessedWeight:N0} of 100 points of evidence; the rest had no data in the period."
                : null;
        }

        // Category threshold from the ASR (P1).
        var category = string.IsNullOrEmpty(supplier.CategoryId) ? null : await categories.GetByIdAsync(supplier.CategoryId);
        review.CategoryMinScore = category?.MinScoreThreshold;
        review.BelowCategoryThreshold = category is not null
            && review.Outcome != ReviewOutcome.NotAssessed
            && review.OverallScore < category.MinScoreThreshold;

        review.ReviewedBy = userId;
        review.ReviewDate = DateTime.UtcNow;
        review.UpdatedBy = userId;
        review.UpdatedAt = DateTime.UtcNow;

        var saved = string.IsNullOrEmpty(review.Id) || await reviews.GetByIdAsync(review.Id) is null
            ? await reviews.CreateAsync(review)
            : await reviews.UpdateAsync(review);

        // ── Feed the ASR (P1): rank the supplier by its latest assessed score ──
        if (saved.Outcome != ReviewOutcome.NotAssessed)
        {
            supplier.OverallScore = saved.OverallScore;
            supplier.LastReviewedAt = saved.ReviewDate;
            supplier.UpdatedBy = userId;
            supplier.UpdatedAt = DateTime.UtcNow;
            await suppliers.UpdateAsync(supplier);
        }
        return saved;
    }

    // ── MD escalation ──
    public async Task<PerformanceActionResult> EscalateAsync(string id, EscalateReviewDto dto, string userId)
    {
        var review = await reviews.GetByIdAsync(id);
        if (review is null) return new PerformanceActionResult("Error", "Review not found.");
        if (review.Outcome != ReviewOutcome.MdEscalation)
            return new PerformanceActionResult("Error", $"Only a score below {EscalationThreshold:N0} escalates to the MD; this review scored {review.OverallScore:N1}.");
        if (review.MdEscalatedAt != null)
            return new PerformanceActionResult("Error", "This review has already been escalated.");

        review.MdEscalatedBy = userId;
        review.MdEscalatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            review.Notes = string.IsNullOrWhiteSpace(review.Notes) ? dto.Notes : $"{review.Notes} {dto.Notes}";
        review.UpdatedBy = userId;
        review.UpdatedAt = DateTime.UtcNow;
        await reviews.UpdateAsync(review);

        await LogAsync(review.Id, AsrAuditAction.PerformanceMdEscalated,
            $"{review.SupplierName} scored {review.OverallScore:N1}/100 in {review.ReviewPeriod} — escalated to the MD for a blacklist decision.", userId);
        return new PerformanceActionResult("Escalated",
            $"Escalated to the MD. Blacklisting remains an MD decision in the supplier register, where a reason is mandatory.");
    }

    // ── Helpers ──
    /// <summary>"YYYY-H1" covers Jan–Jun, "YYYY-H2" covers Jul–Dec.</summary>
    private static bool TryPeriod(string? period, out DateTime start, out DateTime end)
    {
        start = default; end = default;
        if (string.IsNullOrWhiteSpace(period)) return false;
        var parts = period.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var year) || year is < 2000 or > 2200) return false;
        var half = parts[1].Trim().ToUpperInvariant();
        if (half is not ("H1" or "H2")) return false;
        start = half == "H1" ? new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc) : new DateTime(year, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        end = start.AddMonths(6);
        return true;
    }

    private static string CurrentPeriod()
    {
        var now = DateTime.UtcNow;
        return $"{now.Year}-{(now.Month <= 6 ? "H1" : "H2")}";
    }

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private async Task LogAsync(string entityId, AsrAuditAction action, string detail, string userId)
    {
        await audit.CreateAsync(new ProcurementAuditLog
        {
            EntityType = "SupplierPerformanceReview", EntityId = entityId, Action = action,
            Detail = detail, PerformedBy = userId, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
