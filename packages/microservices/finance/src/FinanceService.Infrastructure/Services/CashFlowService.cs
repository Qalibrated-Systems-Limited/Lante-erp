using FinanceService.Core.DTOs;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

public class CashFlowService : ICashFlowService
{
    private readonly FinanceDbContext _db;
    public CashFlowService(FinanceDbContext db) => _db = db;

    public async Task<CashFlowDto> ForecastAsync(DateTime asOf, int weeks = 8)
    {
        asOf = asOf.Date;

        // Cash now = GL balance (debit − credit) of bank/cash accounts up to today.
        var cashAccountIds = await _db.ChartOfAccounts
            .Where(a => a.IsBank || a.Code == "1110").Select(a => a.Id).ToListAsync();
        var cashDr = await _db.GeneralLedgerEntries.Where(g => cashAccountIds.Contains(g.AccountId) && g.EntryDate <= asOf).SumAsync(g => (decimal?)g.BaseDebit) ?? 0;
        var cashCr = await _db.GeneralLedgerEntries.Where(g => cashAccountIds.Contains(g.AccountId) && g.EntryDate <= asOf).SumAsync(g => (decimal?)g.BaseCredit) ?? 0;
        var cashNow = cashDr - cashCr;

        var receivables = await _db.Invoices
            .Where(i => i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled && i.Balance > 0)
            .Select(i => new { i.DueDate, i.Balance }).ToListAsync();
        var payables = await _db.SupplierInvoices
            .Where(b => b.Status != SupplierInvoiceStatus.Received && b.Status != SupplierInvoiceStatus.Cancelled && b.Balance > 0)
            .Select(b => new { b.DueDate, b.Balance }).ToListAsync();

        var w = new List<CashFlowWeekDto>();
        for (var i = 0; i < weeks; i++)
        {
            var start = asOf.AddDays(7 * i);
            var end = start.AddDays(6);
            w.Add(new CashFlowWeekDto { Label = $"W{i + 1}", PeriodStart = start, PeriodEnd = end });
        }
        var horizonEnd = asOf.AddDays(7 * weeks - 1);

        decimal overdueRec = 0, overduePay = 0;
        foreach (var r in receivables)
        {
            if (r.DueDate.Date < asOf) overdueRec += r.Balance;
            else if (r.DueDate.Date <= horizonEnd) w[(int)((r.DueDate.Date - asOf).Days / 7)].ExpectedIn += r.Balance;
        }
        foreach (var p in payables)
        {
            if (p.DueDate.Date < asOf) overduePay += p.Balance;
            else if (p.DueDate.Date <= horizonEnd) w[(int)((p.DueDate.Date - asOf).Days / 7)].ExpectedOut += p.Balance;
        }

        var running = cashNow;
        decimal lowest = cashNow;
        foreach (var week in w)
        {
            week.Net = week.ExpectedIn - week.ExpectedOut;
            running += week.Net;
            week.ProjectedBalance = running;
            if (running < lowest) lowest = running;
        }

        return new CashFlowDto
        {
            AsOf = asOf, CashNow = cashNow, OverdueReceivables = overdueRec, OverduePayables = overduePay,
            LowestProjectedBalance = lowest, Weeks = w,
        };
    }
}
