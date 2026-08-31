using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

/// Processes 9/10 — bank reconciliation. Statement lines are matched to GL bank movements; the
/// book balance is recomputed live from the ledger so posting a bank-only item keeps the identity
/// (adjustedBook = adjustedBank) intact.
public class BankRecService : IBankRecService
{
    private const decimal Eps = 0.01m;
    private readonly FinanceDbContext _db;
    private readonly IJournalService _journals;
    public BankRecService(FinanceDbContext db, IJournalService journals) { _db = db; _journals = journals; }

    public async Task<ReconciliationReadDto> CreateAsync(CreateReconciliationDto dto, string? actor)
    {
        var code = string.IsNullOrWhiteSpace(dto.BankAccountCode) ? "1100" : dto.BankAccountCode!;
        var acct = await _db.ChartOfAccounts.FirstOrDefaultAsync(a => a.Code == code)
            ?? throw new InvalidOperationException($"Bank account {code} not found.");

        var rec = new BankReconciliation
        {
            RefNo = await NextRefAsync(dto.StatementDate.Year),
            BankAccountCode = code, StatementDate = dto.StatementDate.Date,
            StatementClosingBalance = dto.StatementClosingBalance, Notes = dto.Notes,
            BookBalance = await BookBalanceAsync(acct.Id, dto.StatementDate.Date),
            Status = ReconciliationStatus.Draft, CreatedBy = actor,
        };
        foreach (var l in dto.Lines ?? new())
        {
            rec.Lines.Add(new BankStatementLine
            {
                ReconciliationId = rec.Id, TxnDate = l.TxnDate.Date, Description = l.Description,
                Reference = l.Reference, Amount = l.Amount, MatchStatus = StatementLineStatus.Unmatched,
                CreatedBy = actor,
            });
        }
        _db.BankReconciliations.Add(rec);
        await _db.SaveChangesAsync();

        await AutoMatchAsync(rec, acct.Id);
        await _db.SaveChangesAsync();
        return await BuildAsync(rec.Id);
    }

    public Task<ReconciliationReadDto> GetAsync(string id) => BuildAsync(id);

    public async Task<List<ReconciliationSummaryDto>> ListAsync(int limit = 200)
    {
        var recs = await _db.BankReconciliations.Include(r => r.Lines)
            .OrderByDescending(r => r.CreatedAt).Take(limit).ToListAsync();
        var result = new List<ReconciliationSummaryDto>();
        foreach (var r in recs)
        {
            var acctId = await AcctIdAsync(r.BankAccountCode);
            var (_, _, diff, _, _, _, _) = await ComputeAsync(r, acctId);
            result.Add(new ReconciliationSummaryDto
            {
                Id = r.Id, RefNo = r.RefNo, BankAccountCode = r.BankAccountCode, StatementDate = r.StatementDate,
                StatementClosingBalance = r.StatementClosingBalance, Difference = diff, Status = r.Status.ToString(),
            });
        }
        return result;
    }

    public async Task<ReconciliationReadDto> MatchAsync(string id, string lineId, string glEntryId, string? actor)
    {
        var rec = await Load(id);
        if (rec.Status == ReconciliationStatus.Completed) throw new InvalidOperationException("Reconciliation is completed.");
        var line = rec.Lines.FirstOrDefault(l => l.Id == lineId) ?? throw new KeyNotFoundException("Statement line not found.");
        if (line.MatchStatus != StatementLineStatus.Unmatched) throw new InvalidOperationException("Line is already matched.");

        var acctId = await AcctIdAsync(rec.BankAccountCode);
        var gl = await _db.GeneralLedgerEntries.FirstOrDefaultAsync(g => g.Id == glEntryId && g.AccountId == acctId)
            ?? throw new InvalidOperationException("Ledger entry not found on this bank account.");
        if (rec.Lines.Any(l => l.MatchedGlEntryId == glEntryId)) throw new InvalidOperationException("Ledger entry already matched to another line.");
        if (Math.Abs((gl.BaseDebit - gl.BaseCredit) - line.Amount) > Eps)
            throw new InvalidOperationException("Amounts do not match.");

        line.MatchStatus = StatementLineStatus.Matched; line.MatchedGlEntryId = glEntryId; line.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await BuildAsync(id);
    }

    public async Task<ReconciliationReadDto> UnmatchAsync(string id, string lineId, string? actor)
    {
        var rec = await Load(id);
        if (rec.Status == ReconciliationStatus.Completed) throw new InvalidOperationException("Reconciliation is completed.");
        var line = rec.Lines.FirstOrDefault(l => l.Id == lineId) ?? throw new KeyNotFoundException("Statement line not found.");
        if (line.MatchStatus == StatementLineStatus.PostedAsJournal)
            throw new InvalidOperationException("This line was posted as a journal; reverse the journal to undo it.");
        line.MatchStatus = StatementLineStatus.Unmatched; line.MatchedGlEntryId = null; line.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await BuildAsync(id);
    }

    public async Task<ReconciliationReadDto> PostBankItemAsync(string id, string lineId, PostBankItemDto dto, string? actor)
    {
        var rec = await Load(id);
        if (rec.Status == ReconciliationStatus.Completed) throw new InvalidOperationException("Reconciliation is completed.");
        var line = rec.Lines.FirstOrDefault(l => l.Id == lineId) ?? throw new KeyNotFoundException("Statement line not found.");
        if (line.MatchStatus != StatementLineStatus.Unmatched) throw new InvalidOperationException("Only unmatched lines can be posted.");
        if (string.IsNullOrWhiteSpace(dto.ContraAccountCode)) throw new InvalidOperationException("A contra account is required.");

        var mag = Math.Abs(line.Amount);
        var moneyIn = line.Amount > 0;   // e.g. interest income; money out = bank charge
        var lines = moneyIn
            ? new List<CreateJournalLineDto>
              {
                  new() { AccountCode = rec.BankAccountCode, Debit = mag, Credit = 0, Description = line.Description },
                  new() { AccountCode = dto.ContraAccountCode, Debit = 0, Credit = mag, Description = line.Description },
              }
            : new List<CreateJournalLineDto>
              {
                  new() { AccountCode = dto.ContraAccountCode, Debit = mag, Credit = 0, Description = line.Description },
                  new() { AccountCode = rec.BankAccountCode, Debit = 0, Credit = mag, Description = line.Description },
              };

        var jrnl = await _journals.CreateAsync(new CreateJournalDto
        {
            EntryDate = rec.StatementDate,
            Description = $"Bank rec {rec.RefNo} — {line.Description}",
            SourceModule = "Finance-BankRec", SourceDocumentId = rec.Id, PostImmediately = true, Lines = lines,
        }, actor);

        var acctId = await AcctIdAsync(rec.BankAccountCode);
        var glId = await _db.GeneralLedgerEntries
            .Where(g => g.JournalEntryId == jrnl.Id && g.AccountId == acctId)
            .Select(g => g.Id).FirstOrDefaultAsync();

        line.JournalEntryId = jrnl.Id;
        line.MatchedGlEntryId = glId;
        line.MatchStatus = StatementLineStatus.PostedAsJournal;
        line.UpdatedAt = DateTime.UtcNow;
        rec.BookBalance = await BookBalanceAsync(acctId, rec.StatementDate);
        await _db.SaveChangesAsync();
        return await BuildAsync(id);
    }

    public async Task<ReconciliationReadDto> CompleteAsync(string id, string? actor)
    {
        var rec = await Load(id);
        var acctId = await AcctIdAsync(rec.BankAccountCode);
        var (_, _, diff, _, unStmt, _, _) = await ComputeAsync(rec, acctId);
        if (unStmt > 0)
            throw new InvalidOperationException($"Cannot complete — {unStmt} bank line(s) are still unmatched. Match or post each one first.");
        if (Math.Abs(diff) > Eps)
            throw new InvalidOperationException($"Cannot complete — reconciling difference is {diff:N2}. It must be zero.");
        rec.Status = ReconciliationStatus.Completed; rec.CompletedAt = DateTime.UtcNow; rec.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await BuildAsync(id);
    }

    // ── internals ──
    private async Task AutoMatchAsync(BankReconciliation rec, string acctId)
    {
        var gl = await _db.GeneralLedgerEntries
            .Where(g => g.AccountId == acctId && g.EntryDate <= rec.StatementDate)
            .OrderBy(g => g.EntryDate).ToListAsync();
        var used = new HashSet<string>();
        foreach (var line in rec.Lines.Where(l => l.MatchStatus == StatementLineStatus.Unmatched))
        {
            var hit = gl.FirstOrDefault(g => !used.Contains(g.Id) && Math.Abs((g.BaseDebit - g.BaseCredit) - line.Amount) <= Eps);
            if (hit != null)
            {
                line.MatchStatus = StatementLineStatus.Matched;
                line.MatchedGlEntryId = hit.Id;
                used.Add(hit.Id);
            }
        }
    }

    /// Returns (adjustedBook, adjustedBank, difference, matched, unmatchedStmt, unmatchedBook, unmatchedBookEntries).
    private async Task<(decimal, decimal, decimal, int, int, int, List<GeneralLedgerEntry>)> ComputeAsync(BankReconciliation rec, string acctId)
    {
        var bookBalance = await BookBalanceAsync(acctId, rec.StatementDate);
        var matchedGlIds = rec.Lines.Where(l => l.MatchedGlEntryId != null).Select(l => l.MatchedGlEntryId!).ToHashSet();

        var unmatchedStmt = rec.Lines.Where(l => l.MatchStatus == StatementLineStatus.Unmatched).ToList();
        var bankOnlySum = unmatchedStmt.Sum(l => l.Amount);

        var glEntries = await _db.GeneralLedgerEntries
            .Where(g => g.AccountId == acctId && g.EntryDate <= rec.StatementDate).ToListAsync();
        var unmatchedBook = glEntries.Where(g => !matchedGlIds.Contains(g.Id)).ToList();
        var bookOnlySum = unmatchedBook.Sum(g => g.BaseDebit - g.BaseCredit);

        var adjustedBook = bookBalance + bankOnlySum;                // book once bank-only items (charges/interest) are posted
        var adjustedBank = rec.StatementClosingBalance + bookOnlySum; // statement + book-only items (deposits in transit / outstanding cheques)
        var difference = adjustedBank - adjustedBook;                // zero when every timing difference is accounted for

        var matched = rec.Lines.Count(l => l.MatchStatus != StatementLineStatus.Unmatched);
        return (adjustedBook, adjustedBank, difference, matched, unmatchedStmt.Count, unmatchedBook.Count, unmatchedBook);
    }

    private async Task<ReconciliationReadDto> BuildAsync(string id)
    {
        var rec = await Load(id);
        var acctId = await AcctIdAsync(rec.BankAccountCode);
        var (adjBook, adjBank, diff, matched, unStmt, unBook, unBookEntries) = await ComputeAsync(rec, acctId);
        var jrnlIds = unBookEntries.Select(g => g.JournalEntryId).Distinct().ToList();
        var jrnlDesc = await _db.JournalEntries.Where(j => jrnlIds.Contains(j.Id))
            .ToDictionaryAsync(j => j.Id, j => j.Description);

        return new ReconciliationReadDto
        {
            Id = rec.Id, RefNo = rec.RefNo, BankAccountCode = rec.BankAccountCode, StatementDate = rec.StatementDate,
            StatementClosingBalance = rec.StatementClosingBalance, BookBalance = await BookBalanceAsync(acctId, rec.StatementDate),
            Status = rec.Status.ToString(), Notes = rec.Notes,
            AdjustedBookBalance = adjBook, AdjustedBankBalance = adjBank, Difference = diff,
            MatchedCount = matched, UnmatchedStatementCount = unStmt, UnmatchedBookCount = unBook,
            Lines = rec.Lines.OrderBy(l => l.TxnDate).Select(l => new ReconciliationLineDto
            {
                Id = l.Id, TxnDate = l.TxnDate, Description = l.Description, Reference = l.Reference,
                Amount = l.Amount, MatchStatus = l.MatchStatus.ToString(), MatchedGlEntryId = l.MatchedGlEntryId,
            }).ToList(),
            UnmatchedBookEntries = unBookEntries.OrderBy(g => g.EntryDate).Select(g => new GlEntryDto
            {
                Id = g.Id, EntryDate = g.EntryDate, Amount = g.BaseDebit - g.BaseCredit,
                JournalEntryId = g.JournalEntryId, Description = jrnlDesc.GetValueOrDefault(g.JournalEntryId), Matched = false,
            }).ToList(),
        };
    }

    private async Task<decimal> BookBalanceAsync(string acctId, DateTime asOf)
    {
        var q = _db.GeneralLedgerEntries.Where(g => g.AccountId == acctId && g.EntryDate <= asOf);
        return (await q.SumAsync(g => (decimal?)g.BaseDebit) ?? 0m) - (await q.SumAsync(g => (decimal?)g.BaseCredit) ?? 0m);
    }

    private async Task<string> AcctIdAsync(string code) =>
        await _db.ChartOfAccounts.Where(a => a.Code == code).Select(a => a.Id).FirstOrDefaultAsync()
        ?? throw new InvalidOperationException($"Bank account {code} not found.");

    private async Task<BankReconciliation> Load(string id) =>
        await _db.BankReconciliations.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id)
        ?? throw new KeyNotFoundException("Reconciliation not found.");

    private async Task<string> NextRefAsync(int year)
    {
        var count = await _db.BankReconciliations.CountAsync();
        return $"REC-{year}-{(count + 1):D4}";
    }
}
