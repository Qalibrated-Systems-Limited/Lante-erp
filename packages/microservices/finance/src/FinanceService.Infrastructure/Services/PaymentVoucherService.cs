using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

public class PaymentVoucherService : IPaymentVoucherService
{
    private readonly FinanceDbContext _db;
    private readonly IJournalService _journals;
    private readonly IApprovalAuthorityService _authority;
    public PaymentVoucherService(FinanceDbContext db, IJournalService journals, IApprovalAuthorityService authority)
        { _db = db; _journals = journals; _authority = authority; }

    public async Task<string> RequiredAuthorityAsync(decimal amount)
    {
        var tiers = await _db.PaymentApprovalTiers.OrderBy(t => t.StepNumber).ToListAsync();
        var tier = tiers.FirstOrDefault(t => amount >= t.MinAmount && (t.MaxAmount == null || amount <= t.MaxAmount));
        return tier?.RequiredRole ?? "Managing Director";
    }

    public async Task<VoucherReadDto> CreateAsync(CreateVoucherDto dto, string? actor)
    {
        var baseCcy = await _db.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency)
            ?? throw new InvalidOperationException("No base currency configured.");
        string payee; decimal amount; SupplierInvoice? bill = null;

        if (!string.IsNullOrEmpty(dto.SupplierInvoiceId))
        {
            bill = await _db.SupplierInvoices.FirstOrDefaultAsync(b => b.Id == dto.SupplierInvoiceId)
                ?? throw new InvalidOperationException("Supplier invoice not found.");
            if (bill.Status == SupplierInvoiceStatus.Received)
                throw new InvalidOperationException("Approve the supplier invoice before raising a voucher.");
            // #229: the match was flipped to Exception after approval — the GL already carries the
            // liability but Procurement now says the approval shouldn't have happened. Refuse payment
            // until someone resolves it (moves the match off Exception), rather than paying a bill
            // with an open, unreviewed contradiction between the ledger and the match record.
            if (bill.MatchExceptionFlaggedAt != null)
                throw new InvalidOperationException(
                    $"Supplier invoice {bill.InternalNo} has an unresolved 3-way match exception flagged " +
                    $"{bill.MatchExceptionFlaggedAt:u} (after approval) — resolve it in Procurement before raising payment.");
            payee = bill.SupplierName ?? "Supplier";
            amount = dto.Amount ?? bill.Balance;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.Payee) || dto.Amount is not > 0)
                throw new InvalidOperationException("Ad-hoc voucher needs a payee and amount.");
            payee = dto.Payee!; amount = dto.Amount!.Value;
        }
        if (amount <= 0) throw new InvalidOperationException("Voucher amount must be positive.");

        var voucher = new PaymentVoucher
        {
            VoucherNo = await NextNoAsync(DateTime.UtcNow.Year),
            // A voucher paying an existing bill is paid in THAT bill's currency — its Amount is
            // drawn from bill.Balance/dto.Amount, both already in the bill's currency, not base.
            // Defaulting to base here (as this always did) posted a USD bill's payment as a KES
            // journal at parity, silently misstating the ledger by the exchange rate (#226-class).
            // An ad-hoc voucher (no bill) has no currency of its own to name — CreateVoucherDto
            // carries no CurrencyCode — so it stays base, which is correct for that case.
            SupplierInvoiceId = bill?.Id, Payee = payee, Amount = amount, CurrencyId = bill?.CurrencyId ?? baseCcy.Id,
            RequiredAuthority = await RequiredAuthorityAsync(amount),
            Status = VoucherStatus.PendingApproval,
            BankAccountCode = dto.BankAccountCode ?? "1100", CreatedBy = actor,
        };
        _db.PaymentVouchers.Add(voucher);

        // VoucherNo is unique-indexed; retry with a freshly counted number on collision (see
        // InvoiceService.CreateAsync for the full rationale).
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                voucher.VoucherNo = await NextNoAsync(DateTime.UtcNow.Year);
            }
        }
        return await MapWithCurrencyAsync(await Load(voucher.Id));
    }

    public async Task<VoucherReadDto> ApproveAsync(string id, string? actor, ApprovalContext? ctx = null)
    {
        var v = await Load(id);
        if (v.Status != VoucherStatus.PendingApproval)
            throw new InvalidOperationException($"Voucher is {v.Status}; only PendingApproval vouchers can be approved.");
        _authority.Ensure(ctx ?? ApprovalContext.None, v.RequiredAuthority);
        v.Approvals.Add(new PaymentApprovalLog
        {
            VoucherId = v.Id, StepNumber = v.Approvals.Count + 1, ApproverId = actor,
            ApproverRole = v.RequiredAuthority, Action = ApprovalAction.Approved,
        });
        v.Status = VoucherStatus.Approved;
        v.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await MapWithCurrencyAsync(v);
    }

    private async Task<VoucherReadDto> MapWithCurrencyAsync(PaymentVoucher v)
    {
        var code = await _db.Currencies.Where(c => c.Id == v.CurrencyId).Select(c => c.Code).FirstOrDefaultAsync();
        return v.Map(code);
    }

    public async Task<VoucherReadDto> PayAsync(string id, string? actor)
    {
        var v = await Load(id);
        if (v.Status != VoucherStatus.Approved)
            throw new InvalidOperationException($"Voucher is {v.Status}; approve it before paying.");

        // The voucher's own currency, resolved from the document rather than assumed — same
        // reasoning as InvoiceService.IssueAsync/SupplierInvoiceService.ApproveAsync (#226/#264).
        var voucherCurrency = await _db.Currencies.FirstOrDefaultAsync(c => c.Id == v.CurrencyId)
            ?? throw new InvalidOperationException($"Voucher {v.VoucherNo} refers to a currency that no longer exists.");

        var jrnl = await _journals.CreateAsync(new CreateJournalDto
        {
            EntryDate = DateTime.UtcNow.Date,
            CurrencyCode = voucherCurrency.Code,
            Description = $"Payment {v.VoucherNo} — {v.Payee}",
            SourceModule = "Finance-AP", SourceDocumentId = v.Id, PostImmediately = true,
            Lines = new()
            {
                new() { AccountCode = v.PayablesAccountCode, Debit = v.Amount, Credit = 0 },
                new() { AccountCode = v.BankAccountCode, Debit = 0, Credit = v.Amount },
            },
        }, actor);
        v.JournalEntryId = jrnl.Id;
        v.Status = VoucherStatus.Paid;
        v.PaidAt = DateTime.UtcNow;

        if (v.SupplierInvoiceId != null)
        {
            var bill = await _db.SupplierInvoices.FirstOrDefaultAsync(b => b.Id == v.SupplierInvoiceId);
            if (bill != null)
            {
                bill.PaidAmount += v.Amount; bill.Balance -= v.Amount;
                bill.Status = bill.Balance <= 0 ? SupplierInvoiceStatus.Paid : SupplierInvoiceStatus.PartPaid;
                bill.UpdatedAt = DateTime.UtcNow;
            }
        }
        v.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return v.Map(voucherCurrency.Code);
    }

    public async Task<List<VoucherReadDto>> ListAsync(int limit = 200)
    {
        var vs = await _db.PaymentVouchers.Include(x => x.Approvals)
            .OrderByDescending(x => x.CreatedAt).Take(limit).ToListAsync();
        var currencies = await _db.Currencies.ToDictionaryAsync(c => c.Id, c => c.Code);
        return vs.Select(v => v.Map(currencies.TryGetValue(v.CurrencyId, out var ccy) ? ccy : null)).ToList();
    }

    private async Task<PaymentVoucher> Load(string id) =>
        await _db.PaymentVouchers.Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id)
        ?? throw new KeyNotFoundException("Voucher not found.");

    private async Task<string> NextNoAsync(int year)
    {
        var count = await _db.PaymentVouchers.CountAsync();
        return $"PV-{year}-{(count + 1):D4}";
    }
}

internal static class VoucherMapExtensions
{
    public static VoucherReadDto Map(this PaymentVoucher v, string? currencyCode = null) => new()
    {
        Id = v.Id, VoucherNo = v.VoucherNo, SupplierInvoiceId = v.SupplierInvoiceId, Payee = v.Payee,
        Amount = v.Amount, CurrencyCode = currencyCode, RequiredAuthority = v.RequiredAuthority, Status = v.Status.ToString(),
        JournalEntryId = v.JournalEntryId, PaidAt = v.PaidAt,
        Approvals = v.Approvals.OrderBy(a => a.StepNumber).Select(a => new ApprovalLogDto
        {
            StepNumber = a.StepNumber, ApproverId = a.ApproverId, ApproverRole = a.ApproverRole,
            Action = a.Action.ToString(), ActionedAt = a.ActionedAt,
        }).ToList(),
    };
}
