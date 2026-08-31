using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.DTOs.Quotations;
using ProcurementService.Core.Entities;
using ProcurementService.Core.Enums;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Core.Services;

/// <summary>P3 — quotation &amp; comparative analysis. Enforces the QSL threshold-based sourcing policy: the PR
/// total sets the required quote count, quotes must come from ASR-approved suppliers, and the LPO stays
/// blocked until the minimum is met. Completing the comparison stamps the PR (unblocking P4).</summary>
public class QuotationService(
    IGenericRepository<Quotation> quotations,
    IGenericRepository<QuotationComparison> comparisons,
    IGenericRepository<PurchaseRequisition> prs,
    IGenericRepository<Supplier> suppliers,
    IGenericRepository<ProcurementAuditLog> audit,
    IMapper mapper) : IQuotationService
{
    // QSL Procurement Policy thresholds (KES).
    private const decimal Band1 = 10_000m;    // ≤ → direct LPO, no quote
    private const decimal Band2 = 100_000m;   // ≤ → 1 quote
    private const decimal Band3 = 500_000m;   // ≤ → 2 quotes; above → 3 quotes + MD

    public async Task<SourcingInfoDto> GetSourcingAsync(string prId)
    {
        var pr = await prs.GetByIdAsync(prId) ?? throw new KeyNotFoundException("Requisition not found.");
        var band = BandFor(pr.TotalEstimated);
        return new SourcingInfoDto
        {
            PrId = prId,
            PrTotal = pr.TotalEstimated,
            Band = band.ToString(),
            RequiredQuotes = RequiredQuotes(band),
            MdApprovalRequired = band == SourcingBand.ThreeQuotesMd,
        };
    }

    public async Task<ComparisonDto?> GetComparisonAsync(string prId)
    {
        var pr = await prs.GetByIdAsync(prId);
        if (pr is null) return null;
        var band = BandFor(pr.TotalEstimated);
        var required = RequiredQuotes(band);

        var quotes = await quotations.Query().AsNoTracking().Include(q => q.Lines)
            .Where(q => q.PrId == prId).OrderByDescending(q => q.TotalScore).ThenBy(q => q.TotalQuoted).ToListAsync();
        var cmp = await comparisons.Query().AsNoTracking().FirstOrDefaultAsync(c => c.PrId == prId);

        return new ComparisonDto
        {
            PrId = prId,
            Band = band.ToString(),
            RequiredQuotes = required,
            ReceivedQuotes = quotes.Count,
            MdApprovalRequired = band == SourcingBand.ThreeQuotesMd,
            Status = (cmp?.Status ?? ComparisonStatus.Draft).ToString(),
            RecommendedSupplierId = cmp?.RecommendedSupplierId,
            RecommendedSupplierName = cmp?.RecommendedSupplierName,
            RecommendedQuotationId = cmp?.RecommendedQuotationId,
            SelectionReason = cmp?.SelectionReason,
            OverallScore = cmp?.OverallScore,
            CompletedAt = cmp?.CompletedAt,
            Quotations = await WithPerformanceScoresAsync(quotes),
        };
    }

    /// <summary>P9 feedback into P3 — carries each bidder's latest performance score onto the comparison, so
    /// the recommendation can weigh past performance and not price alone.</summary>
    private async Task<List<QuotationDto>> WithPerformanceScoresAsync(List<Quotation> quotes)
    {
        var dtos = mapper.Map<List<QuotationDto>>(quotes);
        if (dtos.Count == 0) return dtos;

        var supplierIds = quotes.Select(q => q.SupplierId).Distinct().ToList();
        var scores = await suppliers.Query().AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.OverallScore, s.LastReviewedAt })
            .ToListAsync();

        foreach (var dto in dtos)
        {
            var s = scores.FirstOrDefault(x => x.Id == dto.SupplierId);
            dto.SupplierOverallScore = s?.OverallScore;
            dto.SupplierLastReviewedAt = s?.LastReviewedAt;
        }
        return dtos;
    }

    public async Task<QuotationActionResult> RecordQuotationAsync(string prId, RecordQuotationDto dto, string userId)
    {
        var pr = await prs.GetByIdAsync(prId);
        if (pr is null) return new QuotationActionResult("Error", "Requisition not found.");
        if (pr.Status != PrStatus.Approved)
            return new QuotationActionResult("Error", "Quotes can only be recorded against an approved requisition.");

        // ASR gate — only approved, non-blacklisted suppliers.
        var supplier = await suppliers.GetByIdAsync(dto.SupplierId);
        if (supplier is null) return new QuotationActionResult("Error", "Supplier not found.");
        if (!supplier.IsApproved || supplier.BlacklistFlag)
            return new QuotationActionResult("Error", "Only ASR-approved, non-blacklisted suppliers may quote.");

        var lines = dto.Lines.Select(l => new QuotationLine
        {
            ItemDescription = l.ItemDescription.Trim(),
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineTotal = l.Quantity * l.UnitPrice,
            CreatedBy = userId,
            UpdatedBy = userId,
        }).ToList();
        var total = dto.TotalQuoted ?? lines.Sum(l => l.LineTotal);

        var q = await quotations.CreateAsync(new Quotation
        {
            QuoteNumber = await GenerateNumberAsync(),
            PrId = prId,
            SupplierId = dto.SupplierId,
            SupplierName = supplier.Name,
            QuoteDate = dto.QuoteDate ?? DateTime.UtcNow,
            ValidityDays = dto.ValidityDays ?? 30,
            TotalQuoted = total,
            Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "KES" : dto.Currency!,
            QuotePdfUrl = dto.QuotePdfUrl,
            Notes = dto.Notes,
            Lines = lines,
            CreatedBy = userId,
            UpdatedBy = userId,
        });
        await LogAsync(prId, "Quotation", AsrAuditAction.QuotationRecorded, $"Quote {q.QuoteNumber} recorded from {supplier.Name} ({total:N0}).", userId);
        return new QuotationActionResult("Ok", $"Quotation {q.QuoteNumber} recorded.");
    }

    public async Task<QuotationActionResult> ScoreQuotationAsync(string quotationId, ScoreQuotationDto dto, string userId)
    {
        var q = await quotations.GetByIdAsync(quotationId);
        if (q is null) return new QuotationActionResult("Error", "Quotation not found.");
        q.PriceScore = Clamp(dto.PriceScore);
        q.QualityScore = Clamp(dto.QualityScore);
        q.DeliveryScore = Clamp(dto.DeliveryScore);
        // Weighted blend: price 40%, quality 30%, delivery 30%.
        q.TotalScore = Math.Round(q.PriceScore.Value * 0.4m + q.QualityScore.Value * 0.3m + q.DeliveryScore.Value * 0.3m, 2);
        Touch(q, userId);
        await quotations.UpdateAsync(q);
        return new QuotationActionResult("Ok", $"Scored {q.TotalScore:N0}/100.");
    }

    public async Task<QuotationActionResult> DeleteQuotationAsync(string quotationId, string userId)
    {
        var q = await quotations.GetByIdAsync(quotationId);
        if (q is null) return new QuotationActionResult("Error", "Quotation not found.");
        q.IsDeleted = true;
        Touch(q, userId);
        await quotations.UpdateAsync(q);
        return new QuotationActionResult("Ok", "Quotation removed.");
    }

    public async Task<QuotationActionResult> CompleteComparisonAsync(string prId, CompleteComparisonDto dto, string userId)
    {
        var pr = await prs.GetByIdAsync(prId);
        if (pr is null) return new QuotationActionResult("Error", "Requisition not found.");
        if (pr.Status != PrStatus.Approved)
            return new QuotationActionResult("Error", "Requisition must be approved before completing comparison.");

        var band = BandFor(pr.TotalEstimated);
        var required = RequiredQuotes(band);
        var quotes = await quotations.Query().Where(q => q.PrId == prId).ToListAsync();

        if (quotes.Count < required)
            return new QuotationActionResult("Blocked", $"Sourcing band requires {required} quote(s); only {quotes.Count} received.");

        Quotation? rec = null;
        if (band != SourcingBand.DirectLpo)
        {
            if (string.IsNullOrWhiteSpace(dto.RecommendedQuotationId))
                return new QuotationActionResult("Error", "Select the recommended quotation.");
            rec = quotes.FirstOrDefault(q => q.Id == dto.RecommendedQuotationId);
            if (rec is null) return new QuotationActionResult("Error", "Recommended quotation not found for this requisition.");
            if (string.IsNullOrWhiteSpace(dto.SelectionReason))
                return new QuotationActionResult("Error", "A selection reason is required.");
        }

        // Mark recommendation on the quotes.
        foreach (var q in quotes) { q.IsRecommended = q.Id == rec?.Id; Touch(q, userId); await quotations.UpdateAsync(q); }

        var cmp = await comparisons.Query().FirstOrDefaultAsync(c => c.PrId == prId)
                  ?? new QuotationComparison { PrId = prId, CreatedBy = userId };
        cmp.Band = band;
        cmp.RequiredQuotes = required;
        cmp.RecommendedSupplierId = rec?.SupplierId;
        cmp.RecommendedSupplierName = rec?.SupplierName;
        cmp.RecommendedQuotationId = rec?.Id;
        cmp.SelectionReason = dto.SelectionReason;
        cmp.PriceScore = rec?.PriceScore;
        cmp.QualityScore = rec?.QualityScore;
        cmp.DeliveryScore = rec?.DeliveryScore;
        cmp.OverallScore = rec?.TotalScore;
        cmp.Status = ComparisonStatus.Completed;
        cmp.CompletedAt = DateTime.UtcNow;
        Touch(cmp, userId);
        if (string.IsNullOrEmpty(cmp.Id) || await comparisons.GetByIdAsync(cmp.Id) is null)
            await comparisons.CreateAsync(cmp);
        else
            await comparisons.UpdateAsync(cmp);

        // Stamp the PR so P4 can generate the LPO.
        pr.QuotationComparisonId = cmp.Id;
        Touch(pr, userId);
        await prs.UpdateAsync(pr);

        await LogAsync(prId, "QuotationComparison", AsrAuditAction.ComparisonCompleted,
            $"Comparison completed ({band}) — {(rec != null ? $"recommended {rec.SupplierName}" : "direct LPO")}.", userId);
        return new QuotationActionResult("Completed", "Comparison completed — requisition is ready for LPO generation.");
    }

    // ── Helpers ──
    private static SourcingBand BandFor(decimal total)
        => total <= Band1 ? SourcingBand.DirectLpo
         : total <= Band2 ? SourcingBand.OneQuote
         : total <= Band3 ? SourcingBand.TwoQuotes
         : SourcingBand.ThreeQuotesMd;

    private static int RequiredQuotes(SourcingBand b) => b switch
    {
        SourcingBand.DirectLpo => 0,
        SourcingBand.OneQuote => 1,
        SourcingBand.TwoQuotes => 2,
        SourcingBand.ThreeQuotesMd => 3,
        _ => 0,
    };

    private static decimal Clamp(decimal v) => Math.Max(0, Math.Min(100, v));

    private async Task<string> GenerateNumberAsync()
    {
        var prefix = $"QT-{DateTime.UtcNow.Year}-";
        var count = await quotations.Query().CountAsync(q => q.QuoteNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task LogAsync(string entityId, string entityType, AsrAuditAction action, string detail, string userId)
    {
        await audit.CreateAsync(new ProcurementAuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Detail = detail,
            PerformedBy = userId,
            OccurredAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        });
    }

    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
