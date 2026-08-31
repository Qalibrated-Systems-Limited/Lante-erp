using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Payments;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C13 (P14) — payment/debtor alerts. The log is written by the background worker from Finance
/// data; this service serves that log plus a live snapshot pulled straight from Finance for the caller's
/// tenant schema. When Finance is unreachable the snapshot reports FinanceConnected=false with zeroes.</summary>
public class PaymentAlertService(
    IGenericRepository<PaymentAlertLog> alerts,
    IFinanceReadClient finance,
    IMapper mapper) : IPaymentAlertService
{
    private const int DueSoonDays = 7;

    public async Task<List<PaymentAlertDto>> GetAlertsAsync(string? status, string? type, string? severity)
    {
        var q = alerts.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PaymentAlertStatus>(status, true, out var st)) q = q.Where(a => a.Status == st);
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<PaymentAlertType>(type, true, out var t)) q = q.Where(a => a.AlertType == t);
        if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<PaymentAlertSeverity>(severity, true, out var sv)) q = q.Where(a => a.Severity == sv);
        return mapper.Map<List<PaymentAlertDto>>(await q.OrderByDescending(a => a.CreatedAt).Take(300).ToListAsync());
    }

    public async Task<PaymentAlertSummaryDto> GetSummaryAsync(string schema)
    {
        var openAlerts = await alerts.Query().AsNoTracking().Where(a => a.Status == PaymentAlertStatus.Open).ToListAsync();
        var summary = new PaymentAlertSummaryDto
        {
            FinanceConnected = finance.IsConfigured,
            OpenAlerts = openAlerts.Count,
            CriticalAlerts = openAlerts.Count(a => a.Severity == PaymentAlertSeverity.Critical),
        };
        if (!finance.IsConfigured || string.IsNullOrWhiteSpace(schema)) return summary;

        var now = DateTime.UtcNow;
        var invoices = await finance.GetArInvoicesAsync(schema);
        var suppliers = await finance.GetSupplierInvoicesAsync(schema);
        var vouchers = await finance.GetVouchersAsync(schema);
        var aging = await finance.GetDebtorAgingAsync(schema);

        var overdue = invoices.Where(i => i.Balance > 0 && i.DueDate < now).ToList();
        summary.OverdueReceivables = overdue.Sum(i => i.Balance);
        summary.OverdueInvoiceCount = overdue.Count;

        var dueSoon = suppliers.Where(s => s.Balance > 0 && s.DueDate >= now && (s.DueDate - now).TotalDays <= DueSoonDays).ToList();
        summary.PayablesDueSoon = dueSoon.Sum(s => s.Balance);
        summary.PayablesDueSoonCount = dueSoon.Count;
        summary.VouchersAwaitingAuth = vouchers.Count(IsAwaitingAuthorisation);

        summary.Debtors = new DebtorBucketsDto
        {
            Current = aging.Sum(a => a.Current),
            Days1To30 = aging.Sum(a => a.Days1To30),
            Days31To60 = aging.Sum(a => a.Days31To60),
            Days61Plus = aging.Sum(a => a.Days61Plus),
            Total = aging.Sum(a => a.Total),
        };
        summary.TopDebtors = aging
            .Where(a => a.Total > 0)
            .OrderByDescending(a => a.Days61Plus).ThenByDescending(a => a.Total)
            .Take(5)
            .Select(a => new TopDebtorDto { CustomerId = a.CustomerId, CustomerName = a.CustomerName, Outstanding = a.Total, Over60 = a.Days61Plus })
            .ToList();
        return summary;
    }

    public async Task<PaymentAlertActionResult> AcknowledgeAsync(string id, string userId)
    {
        var a = await alerts.GetByIdAsync(id);
        if (a is null) return new PaymentAlertActionResult("Error", "Alert not found.");
        if (a.Status == PaymentAlertStatus.Acknowledged) return new PaymentAlertActionResult("Error", "Alert already acknowledged.");
        a.Status = PaymentAlertStatus.Acknowledged; a.AcknowledgedBy = userId; a.AcknowledgedAt = DateTime.UtcNow;
        a.UpdatedBy = userId; a.UpdatedAt = DateTime.UtcNow;
        await alerts.UpdateAsync(a);
        return new PaymentAlertActionResult("Acknowledged", "Alert acknowledged.");
    }

    // A voucher awaiting authorisation: not yet paid and not in a terminal state.
    public static bool IsAwaitingAuthorisation(FinanceVoucher v)
        => v.PaidAt is null
        && !v.Status.Contains("paid", StringComparison.OrdinalIgnoreCase)
        && !v.Status.Contains("reject", StringComparison.OrdinalIgnoreCase)
        && !v.Status.Contains("cancel", StringComparison.OrdinalIgnoreCase)
        && !v.Status.Contains("approved", StringComparison.OrdinalIgnoreCase);
}
