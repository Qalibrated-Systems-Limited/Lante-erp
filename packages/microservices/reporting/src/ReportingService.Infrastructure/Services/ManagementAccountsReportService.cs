using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;
using ReportingService.Core.Services;

namespace ReportingService.Infrastructure.Services;

// Report #1: management-accounts. Combines FinanceService's P&L + trial balance, and derives a
// balance sheet server-side by replicating apps/lante_frontend/.../BalanceSheetTab.jsx's grouping
// logic exactly (Asset/Liability/Equity classification, sign flips, current-year profit rollup).
public class ManagementAccountsReportService(
    IFinanceServiceClient finance,
    ILogger<ManagementAccountsReportService> logger) : IManagementAccountsReportService
{
    public async Task<ManagementAccountsReportDto> GetAsync(string? periodId, DateTime? asOf)
    {
        var report = new ManagementAccountsReportDto
        {
            PeriodId = periodId,
            AsOf = asOf ?? DateTime.UtcNow,
        };

        var pnlTask = string.IsNullOrWhiteSpace(periodId)
            ? Task.FromResult<(ProfitLossDto? Data, string? Warning)>((null, "periodId not supplied — profit & loss skipped."))
            : ReportHelpers.SafeCallAsync(() => finance.GetPnlAsync(periodId), "profit & loss", logger);

        var tbTask = ReportHelpers.SafeCallAsync(() => finance.GetTrialBalanceAsync(asOf), "trial balance", logger);

        await Task.WhenAll(pnlTask, tbTask);

        var (pnl, pnlWarning) = pnlTask.Result;
        var (tb, tbWarning) = tbTask.Result;

        report.ProfitAndLoss = pnl;
        report.TrialBalance = tb;
        report.BalanceSheet = tb != null ? BuildBalanceSheet(tb) : null;

        if (pnlWarning != null) report.Warnings.Add(pnlWarning);
        if (tbWarning != null) report.Warnings.Add(tbWarning);
        if (tb == null) report.Warnings.Add("Balance sheet not computed — trial balance unavailable.");

        return report;
    }

    /// <summary>
    /// Exact port of BalanceSheetTab.jsx's client-side grouping:
    ///   net(row)          = row.Debit - row.Credit
    ///   Assets             = rows where Classification == "Asset", balance = net(row)
    ///   Liabilities/Equity = rows where Classification == "Liability"/"Equity", balance = -net(row)
    ///   incomeNet          = sum(-net(row)) over "Income" rows
    ///   expenseNet         = sum(net(row)) over "Expense" rows
    ///   profit             = incomeNet - expenseNet
    ///   TotalEquity        = sum(equity balances) + profit
    ///   Balanced           = round(TotalAssets) == round(TotalLiabilities + TotalEquity)
    /// Any row whose Classification isn't exactly one of those five strings is silently excluded,
    /// matching the frontend (no "Other" bucket).
    /// </summary>
    private static BalanceSheetDto BuildBalanceSheet(TrialBalanceDto tb)
    {
        decimal Net(TrialBalanceRowDto r) => r.Debit - r.Credit;

        BalanceSheetRowDto ToRow(TrialBalanceRowDto r, decimal balance) => new()
        {
            AccountId = r.AccountId,
            AccountCode = r.AccountCode,
            AccountName = r.AccountName,
            Balance = balance,
        };

        var assets = tb.Rows.Where(r => r.Classification == "Asset").Select(r => ToRow(r, Net(r))).ToList();
        var liabilities = tb.Rows.Where(r => r.Classification == "Liability").Select(r => ToRow(r, -Net(r))).ToList();
        var equity = tb.Rows.Where(r => r.Classification == "Equity").Select(r => ToRow(r, -Net(r))).ToList();

        var incomeNet = tb.Rows.Where(r => r.Classification == "Income").Sum(r => -Net(r));
        var expenseNet = tb.Rows.Where(r => r.Classification == "Expense").Sum(Net);
        var profit = incomeNet - expenseNet;

        var totalAssets = assets.Sum(r => r.Balance);
        var totalLiabilities = liabilities.Sum(r => r.Balance);
        var totalEquity = equity.Sum(r => r.Balance) + profit;

        return new BalanceSheetDto
        {
            Assets = assets,
            Liabilities = liabilities,
            Equity = equity,
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            TotalEquity = totalEquity,
            CurrentYearProfit = profit,
            // Rounded to the cent, matching the GL's own to-the-cent balance enforcement
            // (JournalService) — rounding to whole units here previously let a balance sheet
            // off by up to ~1 shilling report as "balanced". See #380.
            IsBalanced = Money.Round(totalAssets) == Money.Round(totalLiabilities + totalEquity),
        };
    }
}
