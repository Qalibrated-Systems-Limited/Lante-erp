using System.Globalization;
using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

/// FIN-025 — statutory remittances. Obligation balances are read live from the GL liability
/// accounts (which payroll/AR credit); recording a remittance posts Dr <liability> / Cr Bank.
public class StatutoryService : IStatutoryService
{
    /// Liability account code → (display name, due day of the following month).
    private static readonly (string Code, string Name, int DueDay)[] Obligations =
    {
        ("2210", "PAYE", 9),
        ("2220", "NSSF", 9),
        ("2230", "SHA (NHIF)", 9),
        ("2240", "Housing Levy", 9),
        ("2250", "Withholding Tax", 20),
        ("2200", "VAT", 20),
    };

    private readonly FinanceDbContext _db;
    private readonly IJournalService _journals;
    public StatutoryService(FinanceDbContext db, IJournalService journals) { _db = db; _journals = journals; }

    public async Task<List<ObligationDto>> GetObligationsAsync(string period)
    {
        var (start, end) = MonthRange(period);
        var today = DateTime.UtcNow.Date;
        var codes = Obligations.Select(o => o.Code).ToList();
        var accounts = await _db.ChartOfAccounts.Where(a => codes.Contains(a.Code))
            .ToDictionaryAsync(a => a.Code, a => a.Id);
        var remittances = await _db.StatutoryRemittances.Where(r => r.Period == period).ToListAsync();

        var result = new List<ObligationDto>();
        foreach (var o in Obligations)
        {
            if (!accounts.TryGetValue(o.Code, out var acctId)) continue;
            var balance = await LiabilityBalanceAsync(acctId, end);
            var rem = remittances.FirstOrDefault(r => r.ObligationCode == o.Code);
            var due = DueDate(period, o.DueDay);
            string status = rem != null ? nameof(RemittanceStatus.Remitted)
                : today > due ? nameof(RemittanceStatus.Overdue)
                : nameof(RemittanceStatus.Pending);
            result.Add(new ObligationDto
            {
                Code = o.Code, Name = o.Name, Period = period,
                OutstandingBalance = balance, RemittedAmount = rem?.Amount ?? 0m,
                DueDate = due, DaysToDue = (int)(due - today).TotalDays,
                Status = status, RemittanceId = rem?.Id,
            });
        }
        return result;
    }

    public async Task<RemittanceReadDto> RemitAsync(RemitStatutoryDto dto, string? actor)
    {
        var o = Obligations.FirstOrDefault(x => x.Code == dto.ObligationCode);
        if (o.Code == null) throw new InvalidOperationException("Unknown statutory obligation.");
        if (string.IsNullOrWhiteSpace(dto.Period)) throw new InvalidOperationException("Period (yyyy-MM) is required.");
        MonthRange(dto.Period); // validates format
        if (await _db.StatutoryRemittances.AnyAsync(r => r.ObligationCode == o.Code && r.Period == dto.Period))
            throw new InvalidOperationException($"{o.Name} for {dto.Period} has already been remitted.");

        var (_, end) = MonthRange(dto.Period);
        var acctId = await _db.ChartOfAccounts.Where(a => a.Code == o.Code).Select(a => a.Id).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Liability account {o.Code} not found.");
        var outstanding = await LiabilityBalanceAsync(acctId, end);
        var amount = dto.Amount ?? outstanding;
        if (amount <= 0) throw new InvalidOperationException("Nothing to remit for this period.");

        var bank = string.IsNullOrWhiteSpace(dto.BankAccountCode) ? "1100" : dto.BankAccountCode!;
        var jrnl = await _journals.CreateAsync(new CreateJournalDto
        {
            EntryDate = DateTime.UtcNow.Date,
            Description = $"Statutory remittance — {o.Name} {dto.Period}",
            SourceModule = "Finance-Statutory", SourceDocumentId = dto.Period, PostImmediately = true,
            Lines = new()
            {
                new() { AccountCode = o.Code, Debit = amount, Credit = 0 },
                new() { AccountCode = bank, Debit = 0, Credit = amount },
            },
        }, actor);

        var rem = new StatutoryRemittance
        {
            RefNo = await NextRefAsync(DateTime.UtcNow.Year),
            ObligationCode = o.Code, ObligationName = o.Name, Period = dto.Period, Amount = amount,
            BankAccountCode = bank, DueDate = DueDate(dto.Period, o.DueDay), RemittedAt = DateTime.UtcNow,
            PaymentReference = dto.PaymentReference ?? $"ITX-{dto.Period}-{o.Code}", JournalEntryId = jrnl.Id,
            CreatedBy = actor,
        };
        _db.StatutoryRemittances.Add(rem);
        await _db.SaveChangesAsync();
        return Map(rem);
    }

    public async Task<List<RemittanceReadDto>> ListRemittancesAsync(int limit = 200)
    {
        var items = await _db.StatutoryRemittances.OrderByDescending(r => r.RemittedAt).Take(limit).ToListAsync();
        return items.Select(Map).ToList();
    }

    // ── internals ──
    private async Task<decimal> LiabilityBalanceAsync(string acctId, DateTime asOf)
    {
        var q = _db.GeneralLedgerEntries.Where(g => g.AccountId == acctId && g.EntryDate <= asOf);
        return (await q.SumAsync(g => (decimal?)g.BaseCredit) ?? 0m) - (await q.SumAsync(g => (decimal?)g.BaseDebit) ?? 0m);
    }

    private static (DateTime start, DateTime end) MonthRange(string period)
    {
        if (!DateTime.TryParseExact(period + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new InvalidOperationException("Period must be yyyy-MM.");
        return (start, start.AddMonths(1).AddDays(-1));
    }

    private static DateTime DueDate(string period, int dueDay)
    {
        var (start, _) = MonthRange(period);
        return start.AddMonths(1).AddDays(dueDay - 1);   // dueDay of the following month
    }

    private async Task<string> NextRefAsync(int year)
    {
        var count = await _db.StatutoryRemittances.CountAsync();
        return $"STAT-{year}-{(count + 1):D4}";
    }

    private static RemittanceReadDto Map(StatutoryRemittance r) => new()
    {
        Id = r.Id, RefNo = r.RefNo, ObligationCode = r.ObligationCode, ObligationName = r.ObligationName,
        Period = r.Period, Amount = r.Amount, DueDate = r.DueDate, RemittedAt = r.RemittedAt,
        PaymentReference = r.PaymentReference, JournalEntryId = r.JournalEntryId,
    };
}
