using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

public class MonthEndService : IMonthEndService
{
    private readonly FinanceDbContext _db;
    public MonthEndService(FinanceDbContext db) => _db = db;

    private static readonly string[] DefaultChecklist =
    {
        "Bank reconciliations completed",
        "Prepayments reviewed & amortised",
        "Accruals raised",
        "Monthly depreciation run posted",
        "Inter-company balances reconciled",
        "Supplier statement reconciliations",
    };

    public async Task<PeriodCloseDto> GetCloseAsync(string periodId)
    {
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new InvalidOperationException("Period not found.");
        var items = await _db.PeriodCloseChecklistItems.Where(i => i.PeriodId == periodId).ToListAsync();
        if (items.Count == 0)
        {
            var now = DateTime.UtcNow;
            items = DefaultChecklist.Select(t => new PeriodCloseChecklistItem
            { PeriodId = periodId, Item = t, CreatedAt = now, UpdatedAt = now }).ToList();
            _db.PeriodCloseChecklistItems.AddRange(items);
            await _db.SaveChangesAsync();
        }
        return Map(period, items);
    }

    public async Task<PeriodCloseDto> ToggleItemAsync(string itemId, bool complete, string? actor)
    {
        var item = await _db.PeriodCloseChecklistItems.FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Checklist item not found.");
        item.IsComplete = complete;
        item.CompletedBy = complete ? actor : null;
        item.CompletedAt = complete ? DateTime.UtcNow : null;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetCloseAsync(item.PeriodId);
    }

    public async Task<PeriodCloseDto> CloseAsync(string periodId, string? actor)
    {
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new InvalidOperationException("Period not found.");
        if (period.Status != PeriodStatus.Open)
            throw new InvalidOperationException($"Period is already {period.Status}.");
        var items = await _db.PeriodCloseChecklistItems.Where(i => i.PeriodId == periodId).ToListAsync();
        if (items.Count == 0 || items.Any(i => !i.IsComplete))
            throw new InvalidOperationException("Complete every checklist item before closing the period.");

        period.Status = PeriodStatus.Closed;
        period.LockedBy = actor;
        period.LockedAt = DateTime.UtcNow;
        _db.PeriodCloseLogs.Add(new PeriodCloseLog { PeriodId = periodId, ClosedBy = actor, ClosedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        return Map(period, items);
    }

    public async Task<PeriodCloseDto> ReopenAsync(string periodId, string? actor)
    {
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new InvalidOperationException("Period not found.");
        period.Status = PeriodStatus.Open;
        period.LockedBy = null; period.LockedAt = null;
        var log = await _db.PeriodCloseLogs.Where(l => l.PeriodId == periodId).OrderByDescending(l => l.ClosedAt).FirstOrDefaultAsync();
        if (log != null) { log.ReopenedBy = actor; log.ReopenedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync();
        var items = await _db.PeriodCloseChecklistItems.Where(i => i.PeriodId == periodId).ToListAsync();
        return Map(period, items);
    }

    public async Task<ProfitLossDto> ProfitAndLossAsync(string periodId)
    {
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new InvalidOperationException("Period not found.");
        var accounts = await _db.ChartOfAccounts.Include(a => a.AccountType).ToListAsync();
        var incomeIds = accounts.Where(a => a.AccountType!.Classification == AccountClassification.Income).Select(a => a.Id).ToHashSet();
        var expenseIds = accounts.Where(a => a.AccountType!.Classification == AccountClassification.Expense).Select(a => a.Id).ToHashSet();
        var nameByAcc = accounts.ToDictionary(a => a.Id, a => a);

        var gl = await _db.GeneralLedgerEntries.Where(g => g.PeriodId == periodId).ToListAsync();
        var costCentres = await _db.CostCenters.ToDictionaryAsync(c => c.Id, c => c.Name);

        var income = new List<PnlLineDto>();
        var expenses = new List<PnlLineDto>();
        foreach (var grp in gl.GroupBy(g => g.AccountId))
        {
            var dr = grp.Sum(x => x.BaseDebit); var cr = grp.Sum(x => x.BaseCredit);
            if (!nameByAcc.TryGetValue(grp.Key, out var acc)) continue;
            if (incomeIds.Contains(grp.Key) && (cr - dr) != 0)
                income.Add(new PnlLineDto { AccountCode = acc.Code, AccountName = acc.Name, Amount = cr - dr });
            else if (expenseIds.Contains(grp.Key) && (dr - cr) != 0)
                expenses.Add(new PnlLineDto { AccountCode = acc.Code, AccountName = acc.Name, Amount = dr - cr });
        }

        var byDept = new List<PnlDepartmentDto>();
        foreach (var grp in gl.GroupBy(g => g.CostCenterId ?? "—"))
        {
            var inc = grp.Where(g => incomeIds.Contains(g.AccountId)).Sum(g => g.BaseCredit - g.BaseDebit);
            var exp = grp.Where(g => expenseIds.Contains(g.AccountId)).Sum(g => g.BaseDebit - g.BaseCredit);
            if (inc == 0 && exp == 0) continue;
            byDept.Add(new PnlDepartmentDto
            {
                CostCentre = grp.Key == "—" ? "Unallocated" : (costCentres.TryGetValue(grp.Key, out var nm) ? nm : "Unallocated"),
                Income = inc, Expense = exp, Net = inc - exp,
            });
        }

        var incomeTotal = income.Sum(x => x.Amount);
        var expenseTotal = expenses.Sum(x => x.Amount);
        return new ProfitLossDto
        {
            PeriodId = period.Id, PeriodName = period.Name,
            IncomeTotal = incomeTotal, ExpenseTotal = expenseTotal, NetProfit = incomeTotal - expenseTotal,
            Income = income.OrderBy(x => x.AccountCode).ToList(),
            Expenses = expenses.OrderBy(x => x.AccountCode).ToList(),
            ByDepartment = byDept,
        };
    }

    private static PeriodCloseDto Map(AccountingPeriod p, List<PeriodCloseChecklistItem> items)
    {
        var all = items.Count > 0 && items.All(i => i.IsComplete);
        return new PeriodCloseDto
        {
            PeriodId = p.Id, PeriodName = p.Name, Status = p.Status.ToString(),
            Checklist = items.OrderBy(i => i.CreatedAt).Select(i => new ChecklistItemDto
            { Id = i.Id, Item = i.Item, IsComplete = i.IsComplete, CompletedBy = i.CompletedBy, CompletedAt = i.CompletedAt }).ToList(),
            AllComplete = all,
            CanClose = all && p.Status == PeriodStatus.Open,
        };
    }
}
