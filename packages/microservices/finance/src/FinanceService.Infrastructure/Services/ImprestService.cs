using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Core.Services;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

/// Process 14 — staff imprest lifecycle: request → approve → disburse → retire, plus the
/// 14-day auto-conversion of unretired balances to payroll-deducted personal advances (FIN-012B/C).
public class ImprestService : IImprestService
{
    private const int RetirementWindowDays = 14;
    private readonly FinanceDbContext _db;
    private readonly IJournalService _journals;
    private readonly IApprovalAuthorityService _authority;
    public ImprestService(FinanceDbContext db, IJournalService journals, IApprovalAuthorityService authority)
        { _db = db; _journals = journals; _authority = authority; }

    public async Task<ImprestReadDto> CreateAsync(CreateImprestDto dto, string? actor)
    {
        if (dto.Amount <= 0) throw new InvalidOperationException("Imprest amount must be positive.");
        if (string.IsNullOrWhiteSpace(dto.EmployeeName)) throw new InvalidOperationException("Employee is required.");

        var ccy = string.IsNullOrEmpty(dto.CurrencyCode)
            ? await _db.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency)
            : await _db.Currencies.FirstOrDefaultAsync(c => c.Code == dto.CurrencyCode);
        ccy ??= await _db.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency)
            ?? throw new InvalidOperationException("No base currency configured.");

        var req = new ImprestRequest
        {
            RefNo = await NextRefAsync(DateTime.UtcNow.Year),
            EmployeeName = dto.EmployeeName.Trim(), EmployeeId = dto.EmployeeId,
            Purpose = dto.Purpose?.Trim() ?? "", Amount = dto.Amount, CurrencyId = ccy.Id,
            Status = ImprestStatus.Requested, RequestedBy = actor, CreatedBy = actor,
        };
        _db.ImprestRequests.Add(req);
        await _db.SaveChangesAsync();
        return Map(req, ccy.Code);
    }

    public async Task<ImprestReadDto> ApproveAsync(string id, string? actor, ApprovalContext? ctx = null)
    {
        var req = await Load(id);
        if (req.Status != ImprestStatus.Requested)
            throw new InvalidOperationException($"Imprest is {req.Status}; only Requested imprests can be approved.");
        _authority.Ensure(ctx ?? ApprovalContext.None, await RequiredAuthorityAsync(req.Amount));
        req.Status = ImprestStatus.Approved; req.ApprovedBy = actor; req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(req, await CurrencyCodeOfAsync(req.CurrencyId));
    }

    public async Task<ImprestReadDto> DisburseAsync(string id, string? actor)
    {
        var req = await Load(id);
        if (req.Status != ImprestStatus.Approved)
            throw new InvalidOperationException($"Imprest is {req.Status}; approve it before disbursing.");

        // The imprest's own currency, resolved from the document — same reasoning as
        // InvoiceService.IssueAsync/SupplierInvoiceService.ApproveAsync/PaymentVoucherService
        // .PayAsync (#226/#264). Omitting this posted a USD imprest's disbursement (and its
        // retirement, below) as a KES journal at parity.
        var imprestCurrency = await _db.Currencies.FirstOrDefaultAsync(c => c.Id == req.CurrencyId)
            ?? throw new InvalidOperationException($"Imprest {req.RefNo} refers to a currency that no longer exists.");

        var now = DateTime.UtcNow;
        var jrnl = await _journals.CreateAsync(new CreateJournalDto
        {
            EntryDate = now.Date,
            CurrencyCode = imprestCurrency.Code,
            Description = $"Imprest {req.RefNo} disbursed to {req.EmployeeName}",
            SourceModule = "Finance-Imprest", SourceDocumentId = req.Id, PostImmediately = true,
            Lines = new()
            {
                new() { AccountCode = req.ImprestAccountCode, Debit = req.Amount, Credit = 0, Description = req.Purpose },
                new() { AccountCode = req.BankAccountCode, Debit = 0, Credit = req.Amount },
            },
        }, actor);

        req.DisburseJournalEntryId = jrnl.Id;
        req.DisbursedAt = now;
        req.DueDate = now.Date.AddDays(RetirementWindowDays);
        req.UnretiredBalance = req.Amount;
        req.RetiredAmount = 0;
        req.Status = ImprestStatus.Disbursed;
        req.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return Map(req, imprestCurrency.Code);
    }

    public async Task<ImprestReadDto> RetireAsync(string id, RetireImprestDto dto, string? actor)
    {
        var req = await Load(id);
        if (req.Status is not (ImprestStatus.Disbursed or ImprestStatus.PartlyRetired))
            throw new InvalidOperationException($"Imprest is {req.Status}; only disbursed imprests can be retired.");

        var lines = dto.Lines?.Where(l => l.Amount > 0).ToList() ?? new();
        if (lines.Count == 0) throw new InvalidOperationException("Provide at least one retirement line with an amount.");
        var total = lines.Sum(l => l.Amount);
        if (total > req.UnretiredBalance + 0.01m)
            throw new InvalidOperationException($"Retirement {total:N2} exceeds the unretired balance {req.UnretiredBalance:N2}.");

        // One journal: a debit per expense line, single credit clearing the imprest asset.
        var jl = lines.Select(l => new CreateJournalLineDto
        {
            AccountCode = string.IsNullOrWhiteSpace(l.ExpenseAccountCode) ? "5500" : l.ExpenseAccountCode!,
            Debit = l.Amount, Credit = 0, Description = l.Description,
        }).ToList();
        jl.Add(new() { AccountCode = req.ImprestAccountCode, Debit = 0, Credit = total });

        var imprestCurrency = await _db.Currencies.FirstOrDefaultAsync(c => c.Id == req.CurrencyId)
            ?? throw new InvalidOperationException($"Imprest {req.RefNo} refers to a currency that no longer exists.");

        var jrnl = await _journals.CreateAsync(new CreateJournalDto
        {
            EntryDate = DateTime.UtcNow.Date,
            CurrencyCode = imprestCurrency.Code,
            Description = $"Imprest {req.RefNo} retirement — {req.EmployeeName}",
            SourceModule = "Finance-Imprest", SourceDocumentId = req.Id, PostImmediately = true, Lines = jl,
        }, actor);

        foreach (var l in lines)
        {
            req.RetirementLines.Add(new ImprestRetirementLine
            {
                ImprestRequestId = req.Id, Description = l.Description, Amount = l.Amount,
                ReceiptUrl = l.ReceiptUrl, ExpenseDate = (l.ExpenseDate ?? DateTime.UtcNow).Date,
                ExpenseAccountCode = string.IsNullOrWhiteSpace(l.ExpenseAccountCode) ? "5500" : l.ExpenseAccountCode!,
                JournalEntryId = jrnl.Id, CreatedBy = actor,
            });
        }
        req.RetiredAmount += total;
        req.UnretiredBalance -= total;
        req.Status = req.UnretiredBalance <= 0.01m ? ImprestStatus.Retired : ImprestStatus.PartlyRetired;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(req, imprestCurrency.Code);
    }

    public async Task<List<PersonalAdvanceReadDto>> RunConversionsAsync(DateTime? asOf, string? actor)
    {
        var cutoff = (asOf ?? DateTime.UtcNow).Date;
        var overdue = await _db.ImprestRequests
            .Where(r => (r.Status == ImprestStatus.Disbursed || r.Status == ImprestStatus.PartlyRetired)
                        && r.DueDate != null && r.DueDate < cutoff && r.UnretiredBalance > 0)
            .ToListAsync();

        var currencies = await _db.Currencies.ToDictionaryAsync(c => c.Id, c => c);

        var created = new List<PersonalAdvance>();
        foreach (var r in overdue)
        {
            // #302: PersonalAdvance has no currency of its own — it's recovered through payroll,
            // which pays in base currency (HR's DisciplineService sums OutstandingAdvanceDto.Amount
            // straight into a KES separation-clearance figure). r.UnretiredBalance is in the
            // IMPREST's own currency, so a USD imprest converted here without this step would
            // under-recover at separation by the exchange rate — the same #226 class of defect as
            // the disbursement/retirement postings above, just one step further down the pipeline.
            var note = $"Auto-converted from imprest {r.RefNo} — unretired past {RetirementWindowDays}-day window.";
            var amount = r.UnretiredBalance;
            if (currencies.TryGetValue(r.CurrencyId, out var ccy))
            {
                var rate = ccy.IsBaseCurrency ? 1m : ccy.ExchangeRate;
                if (rate > 0m)
                {
                    amount = Money.Round(r.UnretiredBalance * rate);
                    if (!ccy.IsBaseCurrency)
                        note += $" Converted from {ccy.Code} {r.UnretiredBalance:N2} at {rate:N4}.";
                }
                else
                {
                    // A foreign currency with no (or a zeroed-out) exchange rate — same situation
                    // JournalService.CreateAsync refuses outright for a live post. This is an
                    // unattended batch job covering potentially many employees; abandoning the whole
                    // run over one bad currency would block every other employee's correct
                    // conversion. Flag loudly on the row instead so it is caught at review rather
                    // than silently misstating what this one employee owes.
                    note += $" UNCONVERTED — {ccy.Code} has no valid exchange rate; this figure is still in {ccy.Code}, not KES. Review manually.";
                }
            }
            else
            {
                note += " UNCONVERTED — the imprest's currency record no longer exists. Review manually.";
            }

            // The unretired imprest and the advance both sit in Staff Imprest & Advances (1220) — no GL
            // movement. This reclassifies the balance for HR to recover through the next payroll.
            var adv = new PersonalAdvance
            {
                ImprestRequestId = r.Id, EmployeeName = r.EmployeeName, EmployeeId = r.EmployeeId,
                Amount = amount, ConvertedAt = cutoff, Status = AdvanceStatus.Pending,
                DeductionMonth = cutoff.ToString("yyyy-MM"),
                Notes = note,
                CreatedBy = actor,
            };
            _db.PersonalAdvances.Add(adv);
            created.Add(adv);
            r.Status = ImprestStatus.Converted;
            r.UpdatedAt = DateTime.UtcNow;
        }
        if (created.Count > 0) await _db.SaveChangesAsync();

        var refs = overdue.ToDictionary(r => r.Id, r => r.RefNo);
        return created.Select(a => MapAdvance(a, refs.GetValueOrDefault(a.ImprestRequestId, ""))).ToList();
    }

    public async Task<List<ImprestReadDto>> ListAsync(int limit = 200)
    {
        var items = await _db.ImprestRequests.OrderByDescending(r => r.CreatedAt).Take(limit).ToListAsync();
        var currencies = await _db.Currencies.ToDictionaryAsync(c => c.Id, c => c.Code);
        return items.Select(r => Map(r, currencies.GetValueOrDefault(r.CurrencyId))).ToList();
    }

    private async Task<string?> CurrencyCodeOfAsync(string currencyId) =>
        await _db.Currencies.Where(c => c.Id == currencyId).Select(c => c.Code).FirstOrDefaultAsync();

    public async Task<List<PersonalAdvanceReadDto>> ListAdvancesAsync(int limit = 200)
    {
        var advs = await _db.PersonalAdvances.OrderByDescending(a => a.ConvertedAt).Take(limit).ToListAsync();
        var refs = await _db.ImprestRequests.ToDictionaryAsync(r => r.Id, r => r.RefNo);
        return advs.Select(a => MapAdvance(a, refs.GetValueOrDefault(a.ImprestRequestId, ""))).ToList();
    }

    /// The role required to approve an imprest of this size (shared payment-authority matrix).
    private async Task<string> RequiredAuthorityAsync(decimal amount)
    {
        var tiers = await _db.PaymentApprovalTiers.OrderBy(t => t.StepNumber).ToListAsync();
        var tier = tiers.FirstOrDefault(t => amount >= t.MinAmount && (t.MaxAmount == null || amount <= t.MaxAmount));
        return tier?.RequiredRole ?? "Managing Director";
    }

    private async Task<ImprestRequest> Load(string id) =>
        await _db.ImprestRequests.Include(r => r.RetirementLines).FirstOrDefaultAsync(r => r.Id == id)
        ?? throw new KeyNotFoundException("Imprest not found.");

    private async Task<string> NextRefAsync(int year)
    {
        var count = await _db.ImprestRequests.CountAsync();
        return $"IMP-{year}-{(count + 1):D4}";
    }

    private static ImprestReadDto Map(ImprestRequest r, string? currencyCode = null) => new()
    {
        Id = r.Id, RefNo = r.RefNo, EmployeeName = r.EmployeeName, Purpose = r.Purpose,
        Amount = r.Amount, CurrencyCode = currencyCode, Status = r.Status.ToString(), DisbursedAt = r.DisbursedAt, DueDate = r.DueDate,
        DaysToDue = r.DueDate == null ? null : (int)(r.DueDate.Value.Date - DateTime.UtcNow.Date).TotalDays,
        RetiredAmount = r.RetiredAmount, UnretiredBalance = r.UnretiredBalance,
    };

    private static PersonalAdvanceReadDto MapAdvance(PersonalAdvance a, string imprestRef) => new()
    {
        Id = a.Id, ImprestRef = imprestRef, EmployeeName = a.EmployeeName, Amount = a.Amount,
        ConvertedAt = a.ConvertedAt, Status = a.Status.ToString(),
    };
}
