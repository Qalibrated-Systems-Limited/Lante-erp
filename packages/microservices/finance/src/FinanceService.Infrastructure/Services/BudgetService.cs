using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

public class BudgetService : IBudgetService
{
    private readonly FinanceDbContext _db;
    public BudgetService(FinanceDbContext db) => _db = db;

    public async Task<BudgetReadDto> CreateBudgetAsync(CreateBudgetDto dto, string? actor)
    {
        var type = Enum.TryParse<BudgetType>(dto.BudgetType, true, out var t) ? t : BudgetType.Annual;

        var prior = await _db.Budgets.Where(x =>
            x.FiscalYearId == dto.FiscalYearId && x.DepartmentName == dto.DepartmentName &&
            x.CostCenterId == dto.CostCenterId && x.IsActive).FirstOrDefaultAsync();
        if (prior != null) prior.IsActive = false;

        var b = new Budget
        {
            FiscalYearId = dto.FiscalYearId, DepartmentName = dto.DepartmentName,
            CostCenterId = dto.CostCenterId, CostCentreLabel = dto.CostCentreLabel,
            AnnualAmount = dto.AnnualAmount, BudgetType = type.ToString(), CreatedBy = actor,
            Version = (prior?.Version ?? 0) + 1, IsActive = true,
        };
        _db.Budgets.Add(b);
        await _db.SaveChangesAsync();
        var (start, end) = await YearRangeAsync(dto.FiscalYearId);
        var expenseIds = await AccountIdsAsync(AccountClassification.Expense);
        return MapBudget(b, await ActualAsync(expenseIds, start, end, b.CostCenterId, expense: true));
    }

    public async Task<List<BudgetReadDto>> ListBudgetsAsync(string fiscalYearId, bool includeSuperseded = false)
    {
        var (start, end) = await YearRangeAsync(fiscalYearId);
        var expenseIds = await AccountIdsAsync(AccountClassification.Expense);
        var budgets = await _db.Budgets
            .Where(b => b.FiscalYearId == fiscalYearId && (includeSuperseded || b.IsActive))
            .ToListAsync();
        var result = new List<BudgetReadDto>();
        foreach (var b in budgets)
            result.Add(MapBudget(b, await ActualAsync(expenseIds, start, end, b.CostCenterId, expense: true)));
        return result;
    }

    public async Task<RevenueTargetReadDto> CreateTargetAsync(CreateRevenueTargetDto dto, string? actor)
    {
        var t = new RevenueTarget
        {
            FiscalYearId = dto.FiscalYearId, Scope = dto.Scope, CostCenterId = dto.CostCenterId,
            AnnualAmount = dto.AnnualAmount, CreatedBy = actor,
        };
        _db.RevenueTargets.Add(t);
        await _db.SaveChangesAsync();
        var (start, end) = await YearRangeAsync(dto.FiscalYearId);
        var incomeIds = await AccountIdsAsync(AccountClassification.Income);
        return MapTarget(t, await ActualAsync(incomeIds, start, end, t.CostCenterId, expense: false));
    }

    public async Task<List<RevenueTargetReadDto>> ListTargetsAsync(string fiscalYearId)
    {
        var (start, end) = await YearRangeAsync(fiscalYearId);
        var incomeIds = await AccountIdsAsync(AccountClassification.Income);
        var targets = await _db.RevenueTargets.Where(t => t.FiscalYearId == fiscalYearId).ToListAsync();
        var result = new List<RevenueTargetReadDto>();
        foreach (var t in targets)
            result.Add(MapTarget(t, await ActualAsync(incomeIds, start, end, t.CostCenterId, expense: false)));
        return result;
    }

    // ── helpers ──
    private async Task<(DateTime start, DateTime end)> YearRangeAsync(string fyId)
    {
        var fy = await _db.FiscalYears.FirstOrDefaultAsync(f => f.Id == fyId)
            ?? throw new InvalidOperationException("Fiscal year not found.");
        return (fy.StartDate, fy.EndDate);
    }

    private async Task<List<string>> AccountIdsAsync(AccountClassification cls) =>
        await _db.ChartOfAccounts.Include(a => a.AccountType)
            .Where(a => a.AccountType!.Classification == cls).Select(a => a.Id).ToListAsync();

    private async Task<decimal> ActualAsync(List<string> accountIds, DateTime start, DateTime end, string? costCenterId, bool expense)
    {
        var q = _db.GeneralLedgerEntries
            .Where(g => accountIds.Contains(g.AccountId) && g.EntryDate >= start && g.EntryDate <= end);
        if (costCenterId != null) q = q.Where(g => g.CostCenterId == costCenterId);
        var dr = await q.SumAsync(g => (decimal?)g.BaseDebit) ?? 0;
        var cr = await q.SumAsync(g => (decimal?)g.BaseCredit) ?? 0;
        return expense ? dr - cr : cr - dr;
    }

    private static BudgetReadDto MapBudget(Budget b, decimal actual)
    {
        var pct = b.AnnualAmount > 0 ? (double)(actual / b.AnnualAmount) * 100 : 0;
        return new BudgetReadDto
        {
            Id = b.Id, DepartmentName = b.DepartmentName, CostCentreLabel = b.CostCentreLabel,
            AnnualAmount = b.AnnualAmount, Actual = actual, Variance = b.AnnualAmount - actual,
            ConsumedPct = Math.Round(pct, 1),
            Status = pct >= 100 ? "Over" : pct >= 80 ? "Warning" : "OnTrack",
            Version = b.Version, BudgetType = b.BudgetType, IsActive = b.IsActive,
        };
    }

    private static RevenueTargetReadDto MapTarget(RevenueTarget t, decimal actual)
    {
        var pct = t.AnnualAmount > 0 ? (double)(actual / t.AnnualAmount) * 100 : 0;
        return new RevenueTargetReadDto
        {
            Id = t.Id, Scope = t.Scope, AnnualAmount = t.AnnualAmount, Actual = actual,
            Variance = actual - t.AnnualAmount, AchievedPct = Math.Round(pct, 1),
            Status = pct >= 100 ? "Achieved" : pct >= 80 ? "OnTrack" : "Behind",
        };
    }
}
