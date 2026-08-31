using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Infrastructure.Data;
using FinanceService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace FinanceService.Tests;

/// <summary>
/// #347 — a revised budget must replace the original as the current budget for its
/// (fiscal year, department, cost centre), not sit alongside it and double-count actuals.
/// </summary>
public sealed class BudgetRevisionTests : IDisposable
{
    private readonly FinanceDbContext _db;
    private readonly BudgetService _sut;
    private const string FyId = "fy-2026";
    private const string ExpenseAccountId = "acc-expense";

    public BudgetRevisionTests()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"budget-tests-{Guid.NewGuid()}")
            .Options;
        _db = new FinanceDbContext(options);

        _db.FiscalYears.Add(new FiscalYear
        {
            Id = FyId, Name = "FY2026",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31),
        });
        var expenseType = new AccountType { Id = "at-expense", Code = "EXP", Name = "Expense", Classification = AccountClassification.Expense };
        _db.AccountTypes.Add(expenseType);
        _db.ChartOfAccounts.Add(new ChartOfAccount { Id = ExpenseAccountId, Code = "5000", Name = "Expense", AccountTypeId = expenseType.Id });
        _db.SaveChanges();

        _sut = new BudgetService(_db);
    }

    private void PostExpense(decimal amount, string? costCenterId = null) =>
        _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
        {
            AccountId = ExpenseAccountId, CostCenterId = costCenterId,
            EntryDate = new DateTime(2026, 6, 1), BaseDebit = amount, BaseCredit = 0m,
        });

    [Fact]
    public async Task Creating_a_revision_supersedes_the_original_instead_of_adding_to_it()
    {
        await _sut.CreateBudgetAsync(new CreateBudgetDto
        {
            FiscalYearId = FyId, DepartmentName = "Operations", AnnualAmount = 1_000_000m, BudgetType = "Annual",
        }, "alice");
        await _sut.CreateBudgetAsync(new CreateBudgetDto
        {
            FiscalYearId = FyId, DepartmentName = "Operations", AnnualAmount = 1_200_000m, BudgetType = "Revised",
        }, "alice");

        var current = await _sut.ListBudgetsAsync(FyId);

        current.Should().ContainSingle();
        current[0].AnnualAmount.Should().Be(1_200_000m);
        current[0].Version.Should().Be(2);
    }

    [Fact]
    public async Task The_superseded_original_is_flagged_inactive_but_not_deleted()
    {
        await _sut.CreateBudgetAsync(new CreateBudgetDto
        {
            FiscalYearId = FyId, DepartmentName = "Operations", AnnualAmount = 1_000_000m,
        }, "alice");
        await _sut.CreateBudgetAsync(new CreateBudgetDto
        {
            FiscalYearId = FyId, DepartmentName = "Operations", AnnualAmount = 1_200_000m, BudgetType = "Revised",
        }, "alice");

        var withHistory = await _sut.ListBudgetsAsync(FyId, includeSuperseded: true);

        withHistory.Should().HaveCount(2);
        withHistory.Should().ContainSingle(b => !b.IsActive && b.AnnualAmount == 1_000_000m && b.Version == 1);
        withHistory.Should().ContainSingle(b => b.IsActive && b.AnnualAmount == 1_200_000m && b.Version == 2);
    }

    [Fact]
    public async Task A_revision_does_not_double_count_actual_spend_against_both_rows()
    {
        PostExpense(900_000m);
        await _db.SaveChangesAsync();

        await _sut.CreateBudgetAsync(new CreateBudgetDto
        {
            FiscalYearId = FyId, DepartmentName = "Operations", AnnualAmount = 1_000_000m,
        }, "alice");
        await _sut.CreateBudgetAsync(new CreateBudgetDto
        {
            FiscalYearId = FyId, DepartmentName = "Operations", AnnualAmount = 1_200_000m, BudgetType = "Revised",
        }, "alice");

        var current = await _sut.ListBudgetsAsync(FyId);

        current.Should().ContainSingle();
        current[0].Actual.Should().Be(900_000m);
    }

    [Fact]
    public async Task Different_departments_do_not_supersede_each_other()
    {
        await _sut.CreateBudgetAsync(new CreateBudgetDto
        {
            FiscalYearId = FyId, DepartmentName = "Operations", AnnualAmount = 1_000_000m,
        }, "alice");
        await _sut.CreateBudgetAsync(new CreateBudgetDto
        {
            FiscalYearId = FyId, DepartmentName = "Finance", AnnualAmount = 500_000m,
        }, "alice");

        var current = await _sut.ListBudgetsAsync(FyId);

        current.Should().HaveCount(2);
    }

    [Fact]
    public async Task BudgetType_is_normalised_regardless_of_the_casing_supplied()
    {
        var created = await _sut.CreateBudgetAsync(new CreateBudgetDto
        {
            FiscalYearId = FyId, DepartmentName = "Operations", AnnualAmount = 1_000_000m, BudgetType = "REVISED",
        }, "alice");

        created.BudgetType.Should().Be("Revised");
    }

    [Fact]
    public async Task An_unrecognised_BudgetType_defaults_to_Annual()
    {
        var created = await _sut.CreateBudgetAsync(new CreateBudgetDto
        {
            FiscalYearId = FyId, DepartmentName = "Operations", AnnualAmount = 1_000_000m, BudgetType = "garbage",
        }, "alice");

        created.BudgetType.Should().Be("Annual");
    }

    public void Dispose() => _db.Dispose();
}
