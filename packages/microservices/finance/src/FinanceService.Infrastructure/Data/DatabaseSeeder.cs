using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Data;

/// Seeds the GL backbone reference data for a tenant: account types, base currencies (KES/USD/CNY),
/// the QSL chart of accounts, and fiscal year 2026 with its 12 monthly periods. Idempotent.
public static class DatabaseSeeder
{
    public static async Task SeedAsync(FinanceDbContext ctx)
    {
        await SeedAccountTypesAsync(ctx);
        await SeedCurrenciesAsync(ctx);
        await SeedChartOfAccountsAsync(ctx);
        await SeedFiscalYearAsync(ctx, 2026);
        await SeedTaxCategoriesAsync(ctx);
        await SeedCustomersAsync(ctx);
        await SeedPaymentTiersAsync(ctx);
        await SeedSuppliersAsync(ctx);
    }

    private static async Task SeedPaymentTiersAsync(FinanceDbContext ctx)
    {
        if (await ctx.PaymentApprovalTiers.AnyAsync()) return;
        var now = DateTime.UtcNow;
        ctx.PaymentApprovalTiers.AddRange(
            new PaymentApprovalTier { MinAmount = 0m,        MaxAmount = 5000m,   RequiredRole = "Staff",             StepNumber = 1, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new PaymentApprovalTier { MinAmount = 5000.01m,  MaxAmount = 20000m,  RequiredRole = "Department Head",   StepNumber = 2, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new PaymentApprovalTier { MinAmount = 20000.01m, MaxAmount = 100000m, RequiredRole = "Finance Manager",   StepNumber = 3, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new PaymentApprovalTier { MinAmount = 100000.01m, MaxAmount = 500000m, RequiredRole = "CFO",              StepNumber = 4, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new PaymentApprovalTier { MinAmount = 500000.01m, MaxAmount = null,    RequiredRole = "Managing Director", StepNumber = 5, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" }
        );
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedSuppliersAsync(FinanceDbContext ctx)
    {
        if (await ctx.Suppliers.AnyAsync()) return;
        var now = DateTime.UtcNow;
        ctx.Suppliers.AddRange(
            new Supplier { Code = "S-001", Name = "Schneider Electric Kenya", KraPin = "P061XXXXXA", CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new Supplier { Code = "S-002", Name = "Total Energies Kenya", KraPin = "P062XXXXXB", CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new Supplier { Code = "S-003", Name = "Nairobi Office Supplies Ltd", KraPin = "P063XXXXXC", CreatedAt = now, UpdatedAt = now, CreatedBy = "system" }
        );
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedTaxCategoriesAsync(FinanceDbContext ctx)
    {
        if (await ctx.TaxCategories.AnyAsync()) return;
        var now = DateTime.UtcNow;
        ctx.TaxCategories.AddRange(
            new TaxCategory { Id = "tax-a", Code = "A", Name = "Standard Rate (16%)", Rate = 0.16m, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new TaxCategory { Id = "tax-b", Code = "B", Name = "Zero Rated (Exports)", Rate = 0m, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new TaxCategory { Id = "tax-c", Code = "C", Name = "Zero Rated (Other)", Rate = 0m, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new TaxCategory { Id = "tax-e", Code = "E", Name = "Exempt", Rate = 0m, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" }
        );
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedCustomersAsync(FinanceDbContext ctx)
    {
        if (await ctx.Customers.AnyAsync()) return;
        var now = DateTime.UtcNow;
        ctx.Customers.AddRange(
            new Customer { Code = "C-001", Name = "Kenya Power & Lighting Co.", KraPin = "P051XXXXXA", IsGovernment = true,  CreditLimit = 5000000, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new Customer { Code = "C-002", Name = "Coast Water Works Dev Agency", KraPin = "P052XXXXXB", IsGovernment = true,  CreditLimit = 3000000, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new Customer { Code = "C-003", Name = "Bamburi Cement Ltd", KraPin = "P053XXXXXC", IsGovernment = false, CreditLimit = 2000000, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" }
        );
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedAccountTypesAsync(FinanceDbContext ctx)
    {
        if (await ctx.AccountTypes.AnyAsync()) return;
        var now = DateTime.UtcNow;
        ctx.AccountTypes.AddRange(
            new AccountType { Id = "at-asset",     Code = "ASSET",     Name = "Asset",     Classification = AccountClassification.Asset,     NormalBalance = NormalBalance.Debit,  CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new AccountType { Id = "at-liability", Code = "LIABILITY", Name = "Liability", Classification = AccountClassification.Liability, NormalBalance = NormalBalance.Credit, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new AccountType { Id = "at-equity",    Code = "EQUITY",    Name = "Equity",    Classification = AccountClassification.Equity,    NormalBalance = NormalBalance.Credit, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new AccountType { Id = "at-income",    Code = "INCOME",    Name = "Income",    Classification = AccountClassification.Income,    NormalBalance = NormalBalance.Credit, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new AccountType { Id = "at-expense",   Code = "EXPENSE",   Name = "Expense",   Classification = AccountClassification.Expense,   NormalBalance = NormalBalance.Debit,  CreatedAt = now, UpdatedAt = now, CreatedBy = "system" }
        );
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedCurrenciesAsync(FinanceDbContext ctx)
    {
        if (await ctx.Currencies.AnyAsync()) return;
        var now = DateTime.UtcNow;
        ctx.Currencies.AddRange(
            new Currency { Code = "KES", Name = "Kenyan Shilling", Symbol = "Kshs", IsBaseCurrency = true,  ExchangeRate = 1m,       CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new Currency { Code = "USD", Name = "US Dollar",       Symbol = "$",    IsBaseCurrency = false, ExchangeRate = 129.50m,  CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            new Currency { Code = "CNY", Name = "Chinese Yuan",    Symbol = "¥",    IsBaseCurrency = false, ExchangeRate = 17.80m,   CreatedAt = now, UpdatedAt = now, CreatedBy = "system" }
        );
        await ctx.SaveChangesAsync();
    }

    // code, name, typeCode, isDirectPosting, isBank
    private static readonly (string Code, string Name, string Type, bool Leaf, bool Bank)[] Coa =
    {
        ("1000","CURRENT ASSETS","ASSET",false,false),
        ("1100","Cash & Bank — Equity Bank (Kshs)","ASSET",true,true),
        ("1101","Cash & Bank — KCB (Kshs)","ASSET",true,true),
        ("1102","Cash & Bank — Stanbic (Kshs)","ASSET",true,true),
        ("1105","Cash & Bank — USD Account","ASSET",true,true),
        ("1110","Petty Cash","ASSET",true,false),
        ("1200","Trade Receivables — Local Clients","ASSET",true,false),
        ("1201","Trade Receivables — Government","ASSET",true,false),
        ("1210","Other Receivables","ASSET",true,false),
        ("1220","Staff Imprest & Advances","ASSET",true,false),
        ("1230","VAT Recoverable (Input VAT)","ASSET",true,false),
        ("1240","Prepayments & Deposits","ASSET",true,false),
        ("1300","Inventory — Instruments & Equipment","ASSET",true,false),
        ("1301","Inventory — Consumables & Spares","ASSET",true,false),
        ("1302","Inventory — PPE & Safety Stock","ASSET",true,false),
        ("1310","Work In Progress — Projects","ASSET",true,false),
        ("1500","NON-CURRENT ASSETS","ASSET",false,false),
        ("1510","Property, Plant & Equipment","ASSET",true,false),
        ("1520","Accumulated Depreciation","ASSET",true,false),
        ("2000","CURRENT LIABILITIES","LIABILITY",false,false),
        ("2100","Trade Payables","LIABILITY",true,false),
        ("2110","Accrued Expenses","LIABILITY",true,false),
        ("2200","VAT Payable (Output VAT)","LIABILITY",true,false),
        ("2210","PAYE Payable","LIABILITY",true,false),
        ("2220","NSSF Payable","LIABILITY",true,false),
        ("2230","SHA Payable","LIABILITY",true,false),
        ("2240","Housing Levy Payable","LIABILITY",true,false),
        ("2250","Withholding Tax Payable","LIABILITY",true,false),
        ("3000","EQUITY","EQUITY",false,false),
        ("3100","Share Capital","EQUITY",true,false),
        ("3200","Retained Earnings","EQUITY",true,false),
        ("3300","Current Year Earnings","EQUITY",true,false),
        ("4000","INCOME","INCOME",false,false),
        ("4100","Calibration Services Revenue","INCOME",true,false),
        ("4110","Engineering Services Revenue","INCOME",true,false),
        ("4120","Inspection Services Revenue","INCOME",true,false),
        ("4900","Other Income","INCOME",true,false),
        ("5000","EXPENSES","EXPENSE",false,false),
        ("5100","Cost of Services","EXPENSE",true,false),
        ("5200","Salaries & Wages","EXPENSE",true,false),
        ("5210","Staff Benefits","EXPENSE",true,false),
        ("5300","Rent & Utilities","EXPENSE",true,false),
        ("5400","Motor Vehicle & Fleet","EXPENSE",true,false),
        ("5500","Administrative Expenses","EXPENSE",true,false),
        ("5600","Depreciation Expense","EXPENSE",true,false),
        ("5900","Forex Gain/Loss","EXPENSE",true,false),
    };

    private static async Task SeedChartOfAccountsAsync(FinanceDbContext ctx)
    {
        if (await ctx.ChartOfAccounts.AnyAsync()) return;
        var now = DateTime.UtcNow;
        var typeByCode = await ctx.AccountTypes.ToDictionaryAsync(t => t.Code, t => t.Id);
        var usdId = (await ctx.Currencies.FirstOrDefaultAsync(c => c.Code == "USD"))?.Id;

        // Header code = first digit block (e.g. 1100 → 1000, 1510 → 1500). Parent is the group header.
        string? ParentCodeOf(string code, bool leaf)
        {
            if (!leaf) return null;
            var group = code[0] + "000";                 // 1000/2000/…
            var subGroup = code[..2] + "00";             // 1500/…
            return Coa.Any(x => x.Code == subGroup && !x.Leaf) ? subGroup : group;
        }

        var byCode = new Dictionary<string, ChartOfAccount>();
        foreach (var a in Coa)
        {
            byCode[a.Code] = new ChartOfAccount
            {
                Code = a.Code, Name = a.Name, AccountTypeId = typeByCode[a.Type],
                IsDirectPosting = a.Leaf, IsBank = a.Bank, IsActive = true,
                CurrencyId = a.Code == "1105" ? usdId : null,
                CreatedAt = now, UpdatedAt = now, CreatedBy = "system",
            };
        }
        foreach (var a in Coa)
        {
            var pc = ParentCodeOf(a.Code, a.Leaf);
            if (pc != null && byCode.TryGetValue(pc, out var parent)) byCode[a.Code].ParentId = parent.Id;
        }
        ctx.ChartOfAccounts.AddRange(byCode.Values);
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedFiscalYearAsync(FinanceDbContext ctx, int year)
    {
        if (await ctx.FiscalYears.AnyAsync(f => f.Name == $"FY{year}")) return;
        var now = DateTime.UtcNow;
        var fy = new FiscalYear
        {
            Name = $"FY{year}",
            StartDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Status = FiscalYearStatus.Open,
            CreatedAt = now, UpdatedAt = now, CreatedBy = "system",
        };
        ctx.FiscalYears.Add(fy);
        for (var m = 1; m <= 12; m++)
        {
            var start = new DateTime(year, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1).AddDays(-1);
            ctx.AccountingPeriods.Add(new AccountingPeriod
            {
                FiscalYearId = fy.Id, PeriodNo = m, Name = $"{year}-{m:D2}",
                StartDate = start, EndDate = end, Status = PeriodStatus.Open,
                CreatedAt = now, UpdatedAt = now, CreatedBy = "system",
            });
        }
        await ctx.SaveChangesAsync();
    }
}
