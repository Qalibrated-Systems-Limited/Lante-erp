using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Quotations;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C4 (P4) — sales quotations. See <see cref="IQuotationService"/>.
/// Distinct from the operations calibration quotation (DEC-3).</summary>
public class QuotationService(
    IGenericRepository<Quotation> quotations,
    IGenericRepository<QuotationLine> lines,
    IGenericRepository<Opportunity> opps,
    IGenericRepository<PriceList> priceLists,
    IGenericRepository<PriceExceptionLog> exceptions,
    IMapper mapper) : IQuotationService
{
    private const decimal MdApprovalThreshold = 500000m;   // CRM-017: MD approves quotes above KES 500k

    // ── Reads ──
    public async Task<QuotationListResult> GetAllAsync(QuotationFilterParams filter)
    {
        var q = quotations.Query().AsNoTracking();
        if (filter.CurrentOnly) q = q.Where(x => x.IsCurrent);
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<QuotationStatus>(filter.Status, true, out var st))
            q = q.Where(x => x.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.OpportunityId)) q = q.Where(x => x.OpportunityId == filter.OpportunityId);
        if (!string.IsNullOrWhiteSpace(filter.CustomerId)) q = q.Where(x => x.CustomerId == filter.CustomerId);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
        return new QuotationListResult(mapper.Map<List<QuotationSummaryDto>>(items), total);
    }

    public async Task<QuotationDetailDto?> GetByIdAsync(string id)
    {
        var quote = await quotations.Query().Include(x => x.Lines).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return quote is null ? null : ToDetail(quote);
    }

    public async Task<List<QuotationSummaryDto>> GetVersionsAsync(string quoteNumber)
    {
        var versions = await quotations.Query().AsNoTracking()
            .Where(x => x.QuoteNumber == quoteNumber).OrderByDescending(x => x.Version).ToListAsync();
        return mapper.Map<List<QuotationSummaryDto>>(versions);
    }

    public async Task<LastSaleInfo> GetLastSaleAsync(string? customerId, string productRef)
    {
        var (price, num, at) = await LastSaleAsync(customerId, productRef, null);
        return new LastSaleInfo(price, num, at);
    }

    // ── Create / edit ──
    public async Task<QuotationDetailDto> CreateAsync(CreateQuotationDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.OpportunityId))
            throw new InvalidOperationException("A quotation must be linked to an opportunity.");
        var opp = await opps.Query().FirstOrDefaultAsync(o => o.Id == dto.OpportunityId)
            ?? throw new KeyNotFoundException("Opportunity not found.");

        // GenerateNumberAsync's count-then-format is racy under concurrent creates; the
        // (QuoteNumber, Version) unique index is the real guard, so a collision here just means
        // another request won the same number first — retry with a freshly counted one.
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var quote = new Quotation
            {
                QuoteNumber = await GenerateNumberAsync(),
                Version = 1,
                IsCurrent = true,
                OpportunityId = opp.Id,
                CustomerId = opp.CustomerId,
                CustomerName = opp.CustomerName,
                Title = string.IsNullOrWhiteSpace(dto.Title) ? opp.Name : dto.Title.Trim(),
                ValidUntil = dto.ValidUntil,
                Notes = dto.Notes?.Trim(),
                VatRate = 0.16m,
                Status = QuotationStatus.Draft,
                CreatedBy = userId, UpdatedBy = userId,
            };
            try
            {
                var created = await quotations.CreateAsync(quote);
                return (await GetByIdAsync(created.Id))!;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                // Almost certainly the (QuoteNumber, Version) unique index rejecting a number
                // another concurrent request just took — fall through and retry with a fresh one.
            }
        }
    }

    public async Task<QuotationDetailDto> SaveLinesAsync(string id, SaveQuotationLinesDto dto, string userId)
    {
        var quote = await quotations.Query().Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == id)
            ?? throw new KeyNotFoundException($"Quotation {id} not found.");
        if (quote.Status != QuotationStatus.Draft)
            throw new InvalidOperationException($"Only a draft quotation can be edited (current: {quote.Status}). Revise to create a new version.");

        // Price-consistency check (CRM-054): compare each line's unit price to the last sale of the
        // same item to this client. A difference requires a documented reason; reasons are logged.
        var priorQuotes = await quotations.Query().Include(q => q.Lines).AsNoTracking()
            .Where(q => q.CustomerId == quote.CustomerId && q.Id != quote.Id
                        && (q.Status == QuotationStatus.Sent || q.Status == QuotationStatus.Accepted))
            .OrderByDescending(q => q.IssueDate).ToListAsync();

        var flagged = new List<PriceExceptionInfo>();
        var toLog = new List<PriceExceptionLog>();
        foreach (var l in dto.Lines)
        {
            var pref = string.IsNullOrWhiteSpace(l.ProductId) ? l.Description.Trim() : l.ProductId;
            var last = FindLastSale(priorQuotes, l.ProductId, l.Description);
            if (last.HasValue && last.Value != l.UnitPrice)
            {
                var variance = last.Value == 0 ? 100 : Math.Round((l.UnitPrice - last.Value) / last.Value * 100, 2);
                if (string.IsNullOrWhiteSpace(l.PriceExceptionReason))
                    flagged.Add(new PriceExceptionInfo(pref, last.Value, l.UnitPrice, variance));
                else
                    toLog.Add(new PriceExceptionLog
                    {
                        QuotationId = quote.Id, CustomerId = quote.CustomerId, ProductRef = pref,
                        LastSalePrice = last.Value, ProposedPrice = l.UnitPrice, VariancePct = variance,
                        Reason = l.PriceExceptionReason!.Trim(), LoggedBy = userId,
                        CreatedBy = userId, UpdatedBy = userId,
                    });
            }
        }
        if (flagged.Count > 0)
            throw new InvalidOperationException(
                "Price differs from the last sale to this client for: " +
                string.Join("; ", flagged.Select(f => $"{f.ProductRef} (last {f.LastSalePrice:0.##} → {f.ProposedPrice:0.##}, {f.VariancePct:+0.##;-0.##}%)")) +
                ". A documented reason is required for each.");

        // Replace lines (soft-delete existing).
        foreach (var old in quote.Lines.Where(x => !x.IsDeleted))
        {
            old.IsDeleted = true; old.UpdatedBy = userId; old.UpdatedAt = DateTime.UtcNow;
            await lines.UpdateAsync(old);
        }
        decimal subtotal = 0, discountAmt = 0;
        foreach (var l in dto.Lines)
        {
            var gross = l.Quantity * l.UnitPrice;
            var disc = Money.Round(gross * l.DiscountPercent / 100);
            // Rounded to cents. `gross` is Quantity * UnitPrice with neither operand constrained, so
            // a fractional quantity carries sub-cent precision straight through to the customer's
            // document: 2.5 units at 10.99 produced a line of 27.475 and a quotation total of 31.875,
            // and 1.5 hours at 3333.33 gave 5799.995. Nothing downstream rounded it either — the
            // money columns have no precision set, so Postgres numeric stored it as-is.
            //
            // The discount above was already rounded to 2dp, which is what says 2dp was the intent
            // all along. Rounding here fixes the whole chain: Subtotal sums 2dp lines, VatAmount was
            // already rounded, and TotalAmount adds two 2dp figures.
            var lineTotal = Money.Round(gross - disc);
            subtotal += lineTotal; discountAmt += disc;
            await lines.CreateAsync(new QuotationLine
            {
                QuotationId = quote.Id, ProductId = l.ProductId, Description = l.Description.Trim(),
                Quantity = l.Quantity, UnitPrice = l.UnitPrice, DiscountPercent = l.DiscountPercent,
                LineTotal = lineTotal, CreatedBy = userId, UpdatedBy = userId,
            });
        }

        var vatRate = dto.VatRate ?? quote.VatRate;
        quote.Subtotal = subtotal;
        quote.DiscountAmount = discountAmt;
        quote.VatRate = vatRate;
        quote.VatAmount = Money.Round(subtotal * vatRate);
        quote.TotalAmount = subtotal + quote.VatAmount;
        if (dto.Title != null) quote.Title = dto.Title.Trim();
        if (dto.ValidUntil.HasValue) quote.ValidUntil = dto.ValidUntil;
        if (dto.Notes != null) quote.Notes = dto.Notes.Trim();
        Touch(quote, userId);
        await quotations.UpdateAsync(quote);

        foreach (var log in toLog) await exceptions.CreateAsync(log);

        return (await GetByIdAsync(id))!;
    }

    // ── Approval workflow ──
    public async Task<QuotationActionResult> SubmitAsync(string id, string userId)
    {
        var quote = await quotations.Query().Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == id)
            ?? throw new KeyNotFoundException("Quotation not found.");
        if (quote.Status != QuotationStatus.Draft) throw new InvalidOperationException($"Only a draft can be submitted (current: {quote.Status}).");
        if (!quote.Lines.Any(l => !l.IsDeleted)) throw new InvalidOperationException("Add at least one line item first.");
        quote.Status = QuotationStatus.PendingDeptHead;
        quote.SubmittedBy = userId; quote.SubmittedAt = DateTime.UtcNow;
        Touch(quote, userId); await quotations.UpdateAsync(quote);
        return new QuotationActionResult(quote.Status.ToString(), "Submitted for Department Head review.");
    }

    public async Task<QuotationActionResult> DeptHeadReviewAsync(string id, bool approve, string? reason, string userId)
    {
        var quote = await Find(id);
        if (quote.Status != QuotationStatus.PendingDeptHead) throw new InvalidOperationException($"Not awaiting Dept Head review (current: {quote.Status}).");
        if (!approve)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A rejection reason is required.");
            quote.Status = QuotationStatus.Draft; quote.RejectionReason = reason.Trim();
            Touch(quote, userId); await quotations.UpdateAsync(quote);
            return new QuotationActionResult(quote.Status.ToString(), "Returned to SE for revision.");
        }
        quote.DeptHeadApprovedBy = userId; quote.DeptHeadApprovedAt = DateTime.UtcNow;
        quote.Status = quote.TotalAmount > MdApprovalThreshold ? QuotationStatus.PendingMd : QuotationStatus.Approved;
        Touch(quote, userId); await quotations.UpdateAsync(quote);
        return new QuotationActionResult(quote.Status.ToString(),
            quote.Status == QuotationStatus.PendingMd ? "Approved by Dept Head — awaiting MD (over KES 500k)." : "Approved.");
    }

    public async Task<QuotationActionResult> MdApproveAsync(string id, string userId)
    {
        var quote = await Find(id);
        if (quote.Status != QuotationStatus.PendingMd) throw new InvalidOperationException($"Not awaiting MD approval (current: {quote.Status}).");
        quote.MdApprovedBy = userId; quote.MdApprovedAt = DateTime.UtcNow; quote.Status = QuotationStatus.Approved;
        Touch(quote, userId); await quotations.UpdateAsync(quote);
        return new QuotationActionResult(quote.Status.ToString(), "Approved by MD.");
    }

    public async Task<QuotationActionResult> SendAsync(string id, string userId)
    {
        var quote = await Find(id);
        if (quote.Status != QuotationStatus.Approved) throw new InvalidOperationException($"Only an approved quotation can be sent (current: {quote.Status}).");
        quote.Status = QuotationStatus.Sent; quote.SentAt = DateTime.UtcNow; quote.IssueDate = DateTime.UtcNow;
        Touch(quote, userId); await quotations.UpdateAsync(quote);
        return new QuotationActionResult(quote.Status.ToString(), "Quotation sent to client.");
    }

    public async Task<QuotationActionResult> RecordOutcomeAsync(string id, QuotationOutcomeDto dto, string userId)
    {
        var quote = await Find(id);
        if (quote.Status != QuotationStatus.Sent) throw new InvalidOperationException($"Only a sent quotation can record an outcome (current: {quote.Status}).");
        quote.Status = dto.Accepted ? QuotationStatus.Accepted : QuotationStatus.Rejected;
        quote.DecidedAt = DateTime.UtcNow;
        if (!dto.Accepted) quote.RejectionReason = dto.Reason?.Trim();
        Touch(quote, userId); await quotations.UpdateAsync(quote);
        return new QuotationActionResult(quote.Status.ToString(), dto.Accepted ? "Client accepted the quotation." : "Client rejected the quotation.");
    }

    public async Task<QuotationDetailDto> ReviseAsync(string id, string userId)
    {
        var src = await quotations.Query().Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == id)
            ?? throw new KeyNotFoundException("Quotation not found.");
        if (src.Status is QuotationStatus.Draft or QuotationStatus.Accepted)
            throw new InvalidOperationException($"A {src.Status} quotation can't be revised.");

        // Retire the current version, spin up a new draft version.
        src.IsCurrent = false; src.Status = QuotationStatus.Superseded;
        Touch(src, userId); await quotations.UpdateAsync(src);

        var next = new Quotation
        {
            QuoteNumber = src.QuoteNumber, Version = src.Version + 1, IsCurrent = true,
            OpportunityId = src.OpportunityId, CustomerId = src.CustomerId, CustomerName = src.CustomerName,
            Title = src.Title, ValidUntil = src.ValidUntil, Notes = src.Notes, VatRate = src.VatRate,
            Subtotal = src.Subtotal, DiscountAmount = src.DiscountAmount, VatAmount = src.VatAmount, TotalAmount = src.TotalAmount,
            Status = QuotationStatus.Draft, CreatedBy = userId, UpdatedBy = userId,
        };
        var created = await quotations.CreateAsync(next);
        foreach (var l in src.Lines.Where(x => !x.IsDeleted))
            await lines.CreateAsync(new QuotationLine
            {
                QuotationId = created.Id, ProductId = l.ProductId, Description = l.Description,
                Quantity = l.Quantity, UnitPrice = l.UnitPrice, DiscountPercent = l.DiscountPercent,
                LineTotal = l.LineTotal, CreatedBy = userId, UpdatedBy = userId,
            });
        return (await GetByIdAsync(created.Id))!;
    }

    // ── Price list ──
    public async Task<List<PriceListDto>> GetPriceListsAsync()
        => mapper.Map<List<PriceListDto>>(await priceLists.Query().AsNoTracking().OrderBy(p => p.ServiceType).ToListAsync());

    public async Task<PriceListDto> SavePriceListAsync(SavePriceListDto dto, string userId)
    {
        var pl = await priceLists.CreateAsync(new PriceList
        {
            ServiceType = dto.ServiceType.Trim(), StandardRate = dto.StandardRate,
            DiscountLimitPercent = dto.DiscountLimitPercent, Currency = dto.Currency ?? "KES",
            CreatedBy = userId, UpdatedBy = userId,
        });
        return mapper.Map<PriceListDto>(pl);
    }

    // ── Helpers ──
    private static decimal? FindLastSale(List<Quotation> priorQuotes, string? productId, string description)
    {
        foreach (var pq in priorQuotes)
        {
            var match = pq.Lines.Where(x => !x.IsDeleted).FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(productId)
                    ? x.ProductId == productId
                    : string.Equals(x.Description, description.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.UnitPrice;
        }
        return null;
    }

    private async Task<(decimal? price, string? num, DateTime? at)> LastSaleAsync(string? customerId, string productRef, string? excludeId)
    {
        if (string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(productRef)) return (null, null, null);
        var priorQuotes = await quotations.Query().Include(q => q.Lines).AsNoTracking()
            .Where(q => q.CustomerId == customerId && q.Id != excludeId
                        && (q.Status == QuotationStatus.Sent || q.Status == QuotationStatus.Accepted))
            .OrderByDescending(q => q.IssueDate).ToListAsync();
        foreach (var pq in priorQuotes)
        {
            var match = pq.Lines.Where(x => !x.IsDeleted).FirstOrDefault(x =>
                x.ProductId == productRef || string.Equals(x.Description, productRef, StringComparison.OrdinalIgnoreCase));
            if (match != null) return (match.UnitPrice, pq.QuoteNumber, pq.IssueDate);
        }
        return (null, null, null);
    }

    private QuotationDetailDto ToDetail(Quotation q)
    {
        var dto = mapper.Map<QuotationDetailDto>(q);
        dto.RequiresMdApproval = q.TotalAmount > MdApprovalThreshold;
        dto.Lines = dto.Lines.OrderBy(l => l.Description).ToList();
        return dto;
    }

    private async Task<Quotation> Find(string id) =>
        await quotations.Query().FirstOrDefaultAsync(q => q.Id == id)
        ?? throw new KeyNotFoundException($"Quotation {id} not found.");

    private async Task<string> GenerateNumberAsync()
    {
        var prefix = $"QT-{DateTime.UtcNow.Year}-";
        // Count distinct quote numbers (versions share a number) → next sequence.
        var nums = await quotations.Query().Where(q => q.QuoteNumber.StartsWith(prefix))
            .Select(q => q.QuoteNumber).Distinct().CountAsync();
        return $"{prefix}{(nums + 1):D4}";
    }

    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
