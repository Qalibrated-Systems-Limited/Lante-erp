using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Core.Services;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

public class SupplierInvoiceService : ISupplierInvoiceService
{
    private readonly FinanceDbContext _db;
    private readonly IJournalService _journals;
    private const string InputVatAccount = "1230";   // VAT Recoverable (Input VAT)

    public SupplierInvoiceService(FinanceDbContext db, IJournalService journals) { _db = db; _journals = journals; }

    public async Task<SupplierInvoiceReadDto> CreateAsync(CreateSupplierInvoiceDto dto, string? actor)
    {
        if (dto.Lines == null || dto.Lines.Count == 0)
            throw new InvalidOperationException("A supplier invoice needs at least one line.");
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == dto.SupplierId)
            ?? throw new InvalidOperationException("Supplier not found.");
        if (!supplier.IsActive)
            throw new InvalidOperationException($"Supplier {supplier.Name} is deactivated — cannot bill from them.");
        var baseCcy = await _db.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency)
            ?? throw new InvalidOperationException("No base currency configured.");
        var ccy = dto.CurrencyCode == null ? baseCcy
            : await _db.Currencies.FirstOrDefaultAsync(c => c.Code == dto.CurrencyCode)
              ?? throw new InvalidOperationException(
                  $"Currency {dto.CurrencyCode} is not configured — add it with an exchange rate before invoicing in it.");
        var taxCats = await _db.TaxCategories.ToListAsync();

        var bill = new SupplierInvoice
        {
            InternalNo = await NextNoAsync(dto.InvoiceDate.Year),
            SupplierInvoiceNo = dto.SupplierInvoiceNo, SupplierId = supplier.Id, SupplierName = supplier.Name,
            InvoiceDate = dto.InvoiceDate, DueDate = dto.DueDate ?? dto.InvoiceDate.AddDays(30),
            CurrencyId = ccy.Id, Status = SupplierInvoiceStatus.Received,
            MatchStatus = dto.LpoReference != null ? ThreeWayMatchStatus.Pending : ThreeWayMatchStatus.NotRequired,
            LpoReference = dto.LpoReference, Notes = dto.Notes, CreatedBy = actor,
        };
        int n = 1;
        foreach (var l in dto.Lines)
        {
            var cat = (l.TaxCode != null ? taxCats.FirstOrDefault(t => t.Code == l.TaxCode) : null)
                      ?? taxCats.FirstOrDefault(t => t.Code == "A")
                      ?? throw new InvalidOperationException("No tax categories seeded.");
            if (!cat.IsActive)
                throw new InvalidOperationException($"Tax category {cat.Code} is deactivated — cannot use it on a new line.");
            var sub = Money.Round(l.Quantity * l.UnitPrice);
            var vat = Money.Round(sub * cat.Rate);
            bill.Lines.Add(new SupplierInvoiceLine
            {
                SupplierInvoiceId = bill.Id, LineNo = n++, Description = l.Description, Quantity = l.Quantity,
                UnitPrice = l.UnitPrice, TaxCategoryId = cat.Id, ExpenseAccountCode = l.ExpenseAccountCode ?? "5100",
                LineSubtotal = sub, VatAmount = vat, LineTotal = sub + vat,
            });
        }
        bill.Subtotal = bill.Lines.Sum(x => x.LineSubtotal);
        bill.VatAmount = bill.Lines.Sum(x => x.VatAmount);
        bill.Total = bill.Subtotal + bill.VatAmount;
        bill.Balance = bill.Total;
        _db.SupplierInvoices.Add(bill);

        // InternalNo is unique-indexed; retry with a freshly counted number on collision (see
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
                bill.InternalNo = await NextNoAsync(dto.InvoiceDate.Year);
            }
        }
        return (await GetAsync(bill.Id))!;
    }

    public async Task<SupplierInvoiceReadDto> ApproveAsync(string id, string? actor)
    {
        var bill = await _db.SupplierInvoices.Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new KeyNotFoundException("Supplier invoice not found.");
        if (bill.Status != SupplierInvoiceStatus.Received)
            throw new InvalidOperationException($"Invoice is {bill.Status}; only Received invoices can be approved.");
        if (bill.MatchStatus == ThreeWayMatchStatus.Exception)
            throw new InvalidOperationException("3-way match has an exception — resolve in Procurement before approval.");

        // The bill's own currency, resolved from the document, so the journal is posted in what the
        // supplier actually billed. Omitting this booked a USD 1,000 bill as a KES 1,000 journal — the AP
        // subledger and the general ledger disagreeing by the exchange rate, with nothing reconciling
        // them (#226).
        var billCurrency = await _db.Currencies.FirstOrDefaultAsync(c => c.Id == bill.CurrencyId)
            ?? throw new InvalidOperationException($"Supplier invoice {bill.InternalNo} refers to a currency that no longer exists.");

        var lines = new List<CreateJournalLineDto>();
        foreach (var g in bill.Lines.GroupBy(l => l.ExpenseAccountCode))
            lines.Add(new CreateJournalLineDto { AccountCode = g.Key, Debit = g.Sum(x => x.LineSubtotal), Credit = 0 });
        if (bill.VatAmount > 0)
            lines.Add(new CreateJournalLineDto { AccountCode = InputVatAccount, Debit = bill.VatAmount, Credit = 0 });
        lines.Add(new CreateJournalLineDto { AccountCode = bill.PayablesAccountCode, Debit = 0, Credit = bill.Total,
            Description = $"{bill.InternalNo} — {bill.SupplierName}" });

        var jrnl = await _journals.CreateAsync(new CreateJournalDto
        {
            EntryDate = bill.InvoiceDate,
            CurrencyCode = billCurrency.Code,
            Description = $"Supplier invoice {bill.InternalNo} — {bill.SupplierName}",
            SourceModule = "Finance-AP", SourceDocumentId = bill.Id, PostImmediately = true, Lines = lines,
        }, actor);
        bill.JournalEntryId = jrnl.Id;
        bill.Status = SupplierInvoiceStatus.Approved;
        bill.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await GetAsync(bill.Id))!;
    }

    public async Task<SupplierInvoiceReadDto> SetMatchStatusAsync(string id, ThreeWayMatchStatus status, string? note, string? actor)
    {
        var bill = await _db.SupplierInvoices.FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new KeyNotFoundException("Supplier invoice not found.");
        if (bill.Status is SupplierInvoiceStatus.Paid or SupplierInvoiceStatus.Cancelled)
            throw new InvalidOperationException($"Invoice is {bill.Status}; its match result can no longer change.");

        bill.MatchStatus = status;

        // #229: a bill whose liability is already posted (Approved/PartPaid) can still legitimately
        // need its match result corrected — Procurement owns the match, and a late GRN or correction
        // should stay recordable. But flipping it to Exception after that point must not be silent:
        // the GL still says "we owe this" while the match now says the approval shouldn't have
        // happened. Flag it and refuse payment (PaymentVoucherService.CreateAsync) until reviewed.
        // Moving the match off Exception again is how the flag is cleared/resolved.
        bill.MatchExceptionFlaggedAt =
            status == ThreeWayMatchStatus.Exception && bill.Status is SupplierInvoiceStatus.Approved or SupplierInvoiceStatus.PartPaid
                ? DateTime.UtcNow
                : null;

        if (!string.IsNullOrWhiteSpace(note))
            bill.Notes = string.IsNullOrWhiteSpace(bill.Notes) ? note : $"{bill.Notes}\n{note}";
        bill.UpdatedBy = actor;
        bill.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await GetAsync(bill.Id))!;
    }

    /// <summary>
    /// Cancel a supplier invoice, reversing its GL posting if it had one.
    ///
    /// <para>The AP mirror of invoice cancellation (#332). <c>SupplierInvoiceStatus.Cancelled</c> was
    /// read at three sites — <c>SetMatchStatusAsync</c>, <c>InputVatForPeriodAsync</c> and
    /// <c>CashFlowService</c> — and assigned by nothing, so a bill entered in error stayed an expense
    /// and a payable for ever.</para>
    ///
    /// <para><b>Why the guard is on PaidAmount.</b> Same reasoning as AR, and the AP side makes it
    /// plainer: <c>PaymentVoucherService</c> sets Paid/PartPaid from the balance when a voucher pays a
    /// bill, exactly as <c>ReceiptService</c> does for invoices. Two services now maintain that status
    /// as a side effect of moving money, and neither exists to protect this method. The money is the
    /// fact; the status is a derived summary of it. Cancelling a bill the business has already paid
    /// would reverse the expense and the payable while the cash stays gone.</para>
    /// </summary>
    public async Task<SupplierInvoiceReadDto> CancelAsync(string id, string reason, string? actor)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A cancellation reason is required.");

        var bill = await _db.SupplierInvoices.FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new KeyNotFoundException("Supplier invoice not found.");

        if (bill.Status == SupplierInvoiceStatus.Cancelled)
            throw new InvalidOperationException($"Supplier invoice {bill.InternalNo} is already cancelled.");

        if (bill.PaidAmount > 0)
            throw new InvalidOperationException(
                $"Supplier invoice {bill.InternalNo} has {bill.PaidAmount:N2} paid against it and cannot "
                + "be cancelled. Reverse the payment voucher first, or record a debit note.");

        if (bill.Status is SupplierInvoiceStatus.Paid or SupplierInvoiceStatus.PartPaid)
            throw new InvalidOperationException(
                $"Supplier invoice {bill.InternalNo} is {bill.Status} and cannot be cancelled.");

        // Received is the pre-approval state and posts nothing, so there is nothing to reverse.
        // Anything past it was posted by ApproveAsync, which writes JournalEntryId and the status in
        // one SaveChanges — read rather than assumed, so a missing journal fails here with something
        // legible instead of a KeyNotFound from inside JournalService.
        if (bill.Status != SupplierInvoiceStatus.Received)
        {
            if (string.IsNullOrEmpty(bill.JournalEntryId))
                throw new InvalidOperationException(
                    $"Supplier invoice {bill.InternalNo} is {bill.Status} but has no journal recorded, so "
                    + "cancelling it would leave its GL posting in place. This needs investigation, not a cancel.");

            // Not caught: a cancelled bill whose expense and input VAT are still recognised is worse
            // than a cancellation that failed and said why. Refuses if the current period is closed (#342).
            await _journals.ReverseAsync(bill.JournalEntryId, actor);
        }

        bill.Status = SupplierInvoiceStatus.Cancelled;
        bill.CancellationReason = reason.Trim();
        bill.CancelledAt = DateTime.UtcNow;
        // Nothing is owed to the supplier now. Written rather than left to be derived, because
        // CashFlowService sums Balance behind its own status filter and a cancelled bill still carrying
        // a balance would be a payable that no report agrees about.
        bill.Balance = 0m;
        bill.UpdatedAt = DateTime.UtcNow;
        bill.UpdatedBy = actor;

        await _db.SaveChangesAsync();
        return (await GetAsync(bill.Id))!;
    }

    public async Task<SupplierInvoiceReadDto?> GetAsync(string id)
    {
        var b = await _db.SupplierInvoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return null;
        var cats = await _db.TaxCategories.ToDictionaryAsync(t => t.Id, t => t.Code);
        var currencies = await _db.Currencies.ToDictionaryAsync(c => c.Id, c => c.Code);
        return Map(b, cats, currencies);
    }

    public async Task<List<SupplierInvoiceReadDto>> ListAsync(int limit = 200)
    {
        var bills = await _db.SupplierInvoices.Include(x => x.Lines)
            .OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.InternalNo).Take(limit).ToListAsync();
        var cats = await _db.TaxCategories.ToDictionaryAsync(t => t.Id, t => t.Code);
        var currencies = await _db.Currencies.ToDictionaryAsync(c => c.Id, c => c.Code);
        return bills.Select(b => Map(b, cats, currencies)).ToList();
    }

    public async Task<decimal> InputVatForPeriodAsync(DateTime start, DateTime end)
        => await _db.SupplierInvoices
            .Where(b => b.Status != SupplierInvoiceStatus.Received && b.Status != SupplierInvoiceStatus.Cancelled
                        && b.InvoiceDate >= start && b.InvoiceDate <= end)
            .SumAsync(b => (decimal?)b.VatAmount) ?? 0;

    private static SupplierInvoiceReadDto Map(SupplierInvoice b, IReadOnlyDictionary<string, string> cats, IReadOnlyDictionary<string, string> currencies) => new()
    {
        Id = b.Id, InternalNo = b.InternalNo, SupplierInvoiceNo = b.SupplierInvoiceNo, SupplierId = b.SupplierId,
        SupplierName = b.SupplierName, InvoiceDate = b.InvoiceDate, DueDate = b.DueDate, Status = b.Status.ToString(),
        CurrencyCode = currencies.TryGetValue(b.CurrencyId, out var ccy) ? ccy : null,
        MatchStatus = b.MatchStatus.ToString(), MatchExceptionFlaggedAt = b.MatchExceptionFlaggedAt,
        LpoReference = b.LpoReference, Subtotal = b.Subtotal, VatAmount = b.VatAmount,
        Total = b.Total, PaidAmount = b.PaidAmount, Balance = b.Balance, JournalEntryId = b.JournalEntryId,
        Notes = b.Notes, CancellationReason = b.CancellationReason, CancelledAt = b.CancelledAt,
        Lines = b.Lines.OrderBy(l => l.LineNo).Select(l => new SupplierInvoiceLineReadDto
        {
            LineNo = l.LineNo, Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
            TaxCode = cats.TryGetValue(l.TaxCategoryId, out var c) ? c : null, ExpenseAccountCode = l.ExpenseAccountCode,
            LineSubtotal = l.LineSubtotal, VatAmount = l.VatAmount, LineTotal = l.LineTotal,
        }).ToList(),
    };

    private async Task<string> NextNoAsync(int year)
    {
        var count = await _db.SupplierInvoices.IgnoreQueryFilters().CountAsync(b => b.InvoiceDate.Year == year);
        return $"BILL-{year}-{(count + 1):D4}";
    }
}
