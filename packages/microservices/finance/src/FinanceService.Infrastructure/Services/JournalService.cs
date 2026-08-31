using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Core.Services;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

public class JournalService : IJournalService
{
    private readonly FinanceDbContext _db;
    public JournalService(FinanceDbContext db) => _db = db;

    public async Task<JournalReadDto> CreateAsync(CreateJournalDto dto, string? actor)
    {
        if (dto.Lines == null || dto.Lines.Count < 2)
            throw new InvalidOperationException("A journal needs at least two lines.");

        var totalDr = dto.Lines.Sum(l => l.Debit);
        var totalCr = dto.Lines.Sum(l => l.Credit);
        if (Money.Round(totalDr) != Money.Round(totalCr))
            throw new InvalidOperationException($"Unbalanced journal: debit {totalDr:N2} ≠ credit {totalCr:N2}.");
        if (totalDr <= 0)
            throw new InvalidOperationException("Journal total must be greater than zero.");

        var period = await _db.AccountingPeriods
            .FirstOrDefaultAsync(p => p.StartDate <= dto.EntryDate && p.EndDate >= dto.EntryDate)
            ?? throw new InvalidOperationException("No accounting period covers that date.");
        if (period.Status != PeriodStatus.Open)
            throw new InvalidOperationException($"Period {period.Name} is {period.Status} — no posting allowed.");

        var baseCcy = await _db.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency)
            ?? throw new InvalidOperationException("No base currency configured.");
        // An unrecognised currency code is REFUSED, never quietly treated as the base currency. Falling back
        // meant a typo — "USE" for "USD" — booked a foreign amount as shillings, balancing perfectly, with
        // nothing to review. Same failure the parity fallback in StubExchangeRateProvider had (#245).
        var ccy = dto.CurrencyCode == null
            ? baseCcy
            : await _db.Currencies.FirstOrDefaultAsync(c => c.Code == dto.CurrencyCode)
              ?? throw new InvalidOperationException(
                  $"Currency {dto.CurrencyCode} is not configured — add it with an exchange rate before posting in it.");
        if (!ccy.IsActive)
            throw new InvalidOperationException($"Currency {ccy.Code} is deactivated — cannot post in it.");

        // A rate of zero or less means nobody has said what this currency is worth. That is not the same as
        // "it is worth exactly one shilling", which is what the old `== 0 ? 1m` fallback asserted: EUR 100
        // was booked as KES 100, a hundredfold understatement that balanced and raised nothing.
        var rate = ccy.ExchangeRate;
        if (rate <= 0m)
        {
            if (!ccy.IsBaseCurrency)
                throw new InvalidOperationException(
                    $"No exchange rate is set for {ccy.Code}. Set one before posting in it — a missing rate cannot be read as parity with {baseCcy.Code}.");
            rate = 1m;   // the base currency is 1 by definition, even if the row was never filled in
        }

        var entry = new JournalEntry
        {
            EntryNo = await NextEntryNoAsync(dto.EntryDate.Year),
            EntryDate = dto.EntryDate,
            PeriodId = period.Id,
            Description = dto.Description,
            CurrencyId = ccy.Id,
            SourceModule = dto.SourceModule,
            SourceDocumentId = dto.SourceDocumentId,
            IsAccrual = dto.IsAccrual,
            AutoReverseDate = dto.AutoReverseDate,
            Status = JournalStatus.Draft,
            PreparedBy = actor,
            TotalDebit = totalDr,
            TotalCredit = totalCr,
        };

        var lineNo = 1;
        foreach (var l in dto.Lines)
        {
            var account = await ResolveAccountAsync(l.AccountId, l.AccountCode);
            if (!account.IsDirectPosting)
                throw new InvalidOperationException($"Account {account.Code} is a header account — cannot post to it.");
            if (l.CostCenterId != null)
            {
                var costCenter = await _db.CostCenters.FirstOrDefaultAsync(x => x.Id == l.CostCenterId)
                    ?? throw new InvalidOperationException($"Cost centre {l.CostCenterId} not found.");
                if (!costCenter.IsActive)
                    throw new InvalidOperationException($"Cost centre {costCenter.Code} is deactivated — cannot assign it to a journal line.");
            }
            entry.Lines.Add(new JournalLine
            {
                JournalEntryId = entry.Id,
                LineNo = lineNo++,
                AccountId = account.Id,
                CostCenterId = l.CostCenterId,
                BranchId = l.BranchId,
                Description = l.Description,
                Debit = l.Debit,
                Credit = l.Credit,
                CurrencyId = ccy.Id,
                FxRate = rate,
                // ExchangeRate is base currency per 1 unit of the foreign currency (USD 131.25 = KES per USD),
                // so converting a foreign amount to base MULTIPLIES. (Was dividing, which turned USD 100 into
                // KES 0.76; it had never fired because every posting to date has been in the base currency.)
                BaseDebit = Money.Round(l.Debit * rate),
                BaseCredit = Money.Round(l.Credit * rate),
            });
        }

        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();

        // Inbound module posts (or explicit request) skip the manual review chain.
        if (dto.PostImmediately) await PostInternalAsync(entry, actor);

        return await GetAsync(entry.Id) ?? throw new InvalidOperationException("Create failed.");
    }

    public async Task<JournalReadDto> SubmitForReviewAsync(string id, string? actor)
        => await Transition(id, JournalStatus.Draft, JournalStatus.PendingReview, actor, e => { });

    public async Task<JournalReadDto> ReviewAsync(string id, string? actor)
        => await Transition(id, JournalStatus.PendingReview, JournalStatus.PendingApproval, actor, e =>
        {
            if (actor != null && actor == e.PreparedBy)
                throw new InvalidOperationException("Reviewer must differ from the preparer (segregation of duties).");
            e.ReviewedBy = actor;
        });

    public async Task<JournalReadDto> ApproveAndPostAsync(string id, string? actor)
    {
        var e = await Load(id);
        if (e.Status != JournalStatus.PendingApproval)
            throw new InvalidOperationException($"Journal is {e.Status}; only PendingApproval entries can be approved.");
        if (actor != null && (actor == e.PreparedBy || actor == e.ReviewedBy))
            throw new InvalidOperationException("Approver must differ from preparer and reviewer.");
        await PostInternalAsync(e, actor);
        return await GetAsync(id) ?? throw new InvalidOperationException("Approve failed.");
    }

    public async Task<JournalReadDto> ReverseAsync(string id, string? actor)
    {
        var e = await Load(id);
        if (e.Status != JournalStatus.Posted)
            throw new InvalidOperationException("Only posted journals can be reversed.");

        // The reversal is dated TODAY, so it must land in the period that covers today — and that
        // period must be open.
        //
        // Neither was true before. This method builds the entry itself and calls PostInternalAsync,
        // which has no period check at all; the only closed-period guard in this service lives in
        // CreateAsync. So a posted journal in a closed month could be reversed straight through the
        // close, which makes the close a suggestion rather than a lock — verified by test, not by
        // reading. Worse, it stamped PeriodId from the ORIGINAL entry while dating itself today, so
        // the GL row carried today's date inside last period's bucket and the two disagreed with
        // nothing reconciling them.
        //
        // Resolving the period from the reversal's own date is not new policy: it is the rule
        // CreateAsync already applies to every other posting. If today's period is closed or absent,
        // the reversal is refused rather than forced somewhere it does not belong — reopening the
        // period is a deliberate act, not a side effect of an undo.
        var reversalDate = DateTime.UtcNow.Date;
        var reversalPeriod = await _db.AccountingPeriods
            .FirstOrDefaultAsync(p => p.StartDate <= reversalDate && p.EndDate >= reversalDate)
            ?? throw new InvalidOperationException(
                $"No accounting period covers {reversalDate:yyyy-MM-dd}, so {e.EntryNo} cannot be reversed today.");
        if (reversalPeriod.Status != PeriodStatus.Open)
            throw new InvalidOperationException(
                $"Period {reversalPeriod.Name} is {reversalPeriod.Status} — {e.EntryNo} cannot be reversed into it.");

        var reversal = new JournalEntry
        {
            EntryNo = await NextEntryNoAsync(reversalDate.Year),
            EntryDate = reversalDate,
            PeriodId = reversalPeriod.Id,
            Description = $"Reversal of {e.EntryNo}: {e.Description}",
            CurrencyId = e.CurrencyId,
            IsReversal = true,
            ReversalOfId = e.Id,
            Status = JournalStatus.Draft,
            PreparedBy = actor,
            TotalDebit = e.TotalCredit,
            TotalCredit = e.TotalDebit,
        };
        foreach (var l in e.Lines.OrderBy(x => x.LineNo))
            reversal.Lines.Add(new JournalLine
            {
                JournalEntryId = reversal.Id, LineNo = l.LineNo, AccountId = l.AccountId,
                CostCenterId = l.CostCenterId, BranchId = l.BranchId, Description = l.Description,
                Debit = l.Credit, Credit = l.Debit, CurrencyId = l.CurrencyId, FxRate = l.FxRate,
                BaseDebit = l.BaseCredit, BaseCredit = l.BaseDebit,
            });
        _db.JournalEntries.Add(reversal);
        e.Status = JournalStatus.Reversed;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await PostInternalAsync(reversal, actor);
        return await GetAsync(reversal.Id) ?? throw new InvalidOperationException("Reverse failed.");
    }

    public async Task<JournalReadDto?> GetAsync(string id)
    {
        var e = await _db.JournalEntries.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return null;
        var accById = await _db.ChartOfAccounts.ToDictionaryAsync(a => a.Id, a => a);
            // One query, not a join per row: Currencies has a handful of rows and is static within a request.
            var ccy = await _db.Currencies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code);
        return new JournalReadDto
        {
            Id = e.Id, EntryNo = e.EntryNo, EntryDate = e.EntryDate, PeriodId = e.PeriodId,
                CurrencyCode = ccy.TryGetValue(e.CurrencyId, out var eCode) ? eCode : null,
            Description = e.Description, Status = e.Status.ToString(), SourceModule = e.SourceModule,
            SourceDocumentId = e.SourceDocumentId, TotalDebit = e.TotalDebit, TotalCredit = e.TotalCredit,
            PreparedBy = e.PreparedBy, ReviewedBy = e.ReviewedBy, ApprovedBy = e.ApprovedBy, PostedAt = e.PostedAt,
            Lines = e.Lines.OrderBy(l => l.LineNo).Select(l => new JournalLineReadDto
            {
                LineNo = l.LineNo, AccountId = l.AccountId,
                AccountCode = accById.TryGetValue(l.AccountId, out var a) ? a.Code : null,
                AccountName = accById.TryGetValue(l.AccountId, out var a2) ? a2.Name : null,
                CostCenterId = l.CostCenterId, BranchId = l.BranchId, Description = l.Description,
                Debit = l.Debit, Credit = l.Credit,
            }).ToList(),
        };
    }

    public async Task<List<JournalReadDto>> ListAsync(int limit = 200)
    {
        var entries = await _db.JournalEntries
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.EntryNo)
            .Take(limit).ToListAsync();
        var ccy = await _db.Currencies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code);
        return entries.Select(e => new JournalReadDto
        {
            Id = e.Id, EntryNo = e.EntryNo, EntryDate = e.EntryDate, PeriodId = e.PeriodId,
                CurrencyCode = ccy.TryGetValue(e.CurrencyId, out var eCode) ? eCode : null,
            Description = e.Description, Status = e.Status.ToString(), SourceModule = e.SourceModule,
            SourceDocumentId = e.SourceDocumentId, TotalDebit = e.TotalDebit, TotalCredit = e.TotalCredit,
            PreparedBy = e.PreparedBy, ReviewedBy = e.ReviewedBy, ApprovedBy = e.ApprovedBy, PostedAt = e.PostedAt,
        }).ToList();
    }

    public async Task<TrialBalanceDto> GetTrialBalanceAsync(DateTime asOf, string? costCenterId, string? branchId)
    {
        var q = _db.GeneralLedgerEntries.Where(g => g.EntryDate <= asOf);
        if (costCenterId != null) q = q.Where(g => g.CostCenterId == costCenterId);
        if (branchId != null) q = q.Where(g => g.BranchId == branchId);

        var sums = await q.GroupBy(g => g.AccountId)
            .Select(grp => new { AccountId = grp.Key, Debit = grp.Sum(x => x.BaseDebit), Credit = grp.Sum(x => x.BaseCredit) })
            .ToListAsync();

        var accounts = await _db.ChartOfAccounts.Include(a => a.AccountType).ToDictionaryAsync(a => a.Id, a => a);
        var rows = new List<TrialBalanceRowDto>();
        foreach (var s in sums)
        {
            if (!accounts.TryGetValue(s.AccountId, out var acc)) continue;
            var net = s.Debit - s.Credit;
            rows.Add(new TrialBalanceRowDto
            {
                AccountId = acc.Id, AccountCode = acc.Code, AccountName = acc.Name,
                Classification = acc.AccountType?.Classification.ToString() ?? "",
                Debit = net > 0 ? net : 0,
                Credit = net < 0 ? -net : 0,
            });
        }
        rows = rows.OrderBy(r => r.AccountCode).ToList();
        var td = rows.Sum(r => r.Debit); var tc = rows.Sum(r => r.Credit);
        return new TrialBalanceDto { AsOf = asOf, Rows = rows, TotalDebit = td, TotalCredit = tc, IsBalanced = Money.Round(td) == Money.Round(tc) };
    }

    // ── helpers ──
    private async Task<JournalEntry> Load(string id) =>
        await _db.JournalEntries.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id)
        ?? throw new KeyNotFoundException($"Journal {id} not found.");

    private async Task<JournalReadDto> Transition(string id, JournalStatus from, JournalStatus to, string? actor, Action<JournalEntry> mutate)
    {
        var e = await Load(id);
        if (e.Status != from) throw new InvalidOperationException($"Journal is {e.Status}; expected {from}.");
        mutate(e);
        e.Status = to;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetAsync(id) ?? throw new InvalidOperationException("Transition failed.");
    }

    private async Task PostInternalAsync(JournalEntry e, string? actor)
    {
        foreach (var l in e.Lines)
            _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                AccountId = l.AccountId, CostCenterId = l.CostCenterId, BranchId = l.BranchId,
                PeriodId = e.PeriodId, JournalEntryId = e.Id, JournalLineId = l.Id, EntryDate = e.EntryDate,
                Debit = l.Debit, Credit = l.Credit, BaseDebit = l.BaseDebit, BaseCredit = l.BaseCredit,
            });
        e.ApprovedBy = actor ?? e.ApprovedBy;
        e.Status = JournalStatus.Posted;
        e.PostedAt = DateTime.UtcNow;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task<ChartOfAccount> ResolveAccountAsync(string? id, string? code)
    {
        ChartOfAccount? a = null;
        if (!string.IsNullOrEmpty(id)) a = await _db.ChartOfAccounts.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null && !string.IsNullOrEmpty(code)) a = await _db.ChartOfAccounts.FirstOrDefaultAsync(x => x.Code == code);
        if (a == null) throw new InvalidOperationException($"Account not found (id={id}, code={code}).");
        if (!a.IsActive) throw new InvalidOperationException($"Account {a.Code} is deactivated — cannot post to it.");
        return a;
    }

    /// The next journal number for a year, derived from the HIGHEST number already issued rather than from the
    /// row count. Counting rows assumes the sequence has no gaps, and any gap — a deleted entry, a purge, a
    /// year's data partially archived — makes it hand out a number that already exists, which then dies on the
    /// EntryNo unique index. Taking the maximum is gap-tolerant and still monotonic.
    private async Task<string> NextEntryNoAsync(int year)
    {
        var prefix = $"JV-{year}-";
        var issued = await _db.JournalEntries.IgnoreQueryFilters()
            .Where(e => e.EntryNo.StartsWith(prefix))
            .Select(e => e.EntryNo)
            .ToListAsync();

        var highest = issued
            .Select(no => int.TryParse(no[prefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D4}";
    }
}
