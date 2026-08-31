using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Core.Services;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

public class InvoiceService : IInvoiceService
{
    private readonly FinanceDbContext _db;
    private readonly IJournalService _journals;
    private readonly IEtimsProvider _etims;
    private const string VatAccount = "2200";

    public InvoiceService(FinanceDbContext db, IJournalService journals, IEtimsProvider etims)
    { _db = db; _journals = journals; _etims = etims; }

    public async Task<InvoiceReadDto> CreateAsync(CreateInvoiceDto dto, string? actor)
    {
        if (dto.Lines == null || dto.Lines.Count == 0)
            throw new InvalidOperationException("An invoice needs at least one line.");
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == dto.CustomerId)
            ?? throw new InvalidOperationException("Customer not found.");
        if (!customer.IsActive)
            throw new InvalidOperationException($"Customer {customer.Name} is deactivated — cannot invoice them.");
        var baseCcy = await _db.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency)
            ?? throw new InvalidOperationException("No base currency configured.");
        var ccy = dto.CurrencyCode == null ? baseCcy
            : await _db.Currencies.FirstOrDefaultAsync(c => c.Code == dto.CurrencyCode)
              ?? throw new InvalidOperationException(
                  $"Currency {dto.CurrencyCode} is not configured — add it with an exchange rate before invoicing in it.");

        var taxCats = await _db.TaxCategories.ToListAsync();
        var inv = new Invoice
        {
            InvoiceNo = await NextNoAsync("INV", dto.InvoiceDate.Year),
            CustomerId = customer.Id, CustomerName = customer.Name,
            InvoiceDate = dto.InvoiceDate, DueDate = dto.DueDate ?? dto.InvoiceDate.AddDays(30),
            CurrencyId = ccy.Id, Status = InvoiceStatus.Draft,
            ReceivableAccountCode = customer.IsGovernment ? "1201" : "1200",
            MilestoneId = dto.MilestoneId, SourceModule = dto.SourceModule, SourceDocumentId = dto.SourceDocumentId,
            Notes = dto.Notes, CreatedBy = actor,
        };
        int n = 1;
        foreach (var l in dto.Lines)
        {
            var cat = (l.TaxCategoryId != null ? taxCats.FirstOrDefault(t => t.Id == l.TaxCategoryId) : null)
                      ?? (l.TaxCode != null ? taxCats.FirstOrDefault(t => t.Code == l.TaxCode) : null)
                      ?? taxCats.FirstOrDefault(t => t.Code == "A")
                      ?? throw new InvalidOperationException("No tax categories seeded.");
            if (!cat.IsActive)
                throw new InvalidOperationException($"Tax category {cat.Code} is deactivated — cannot use it on a new line.");
            var sub = Money.Round(l.Quantity * l.UnitPrice);
            var vat = Money.Round(sub * cat.Rate);
            inv.Lines.Add(new InvoiceLine
            {
                InvoiceId = inv.Id, LineNo = n++, Description = l.Description, Quantity = l.Quantity,
                UnitPrice = l.UnitPrice, TaxCategoryId = cat.Id, RevenueAccountCode = l.RevenueAccountCode ?? "4100",
                LineSubtotal = sub, VatAmount = vat, LineTotal = sub + vat,
            });
        }
        inv.Subtotal = inv.Lines.Sum(x => x.LineSubtotal);
        inv.VatAmount = inv.Lines.Sum(x => x.VatAmount);
        inv.Total = inv.Subtotal + inv.VatAmount;
        inv.Balance = inv.Total;
        _db.Invoices.Add(inv);

        // InvoiceNo is unique-indexed; NextNoAsync's count-then-format is racy under concurrent
        // creates for the same year, so a collision here means another request just took the
        // number this counted — retry with a freshly counted one rather than failing the request.
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
                inv.InvoiceNo = await NextNoAsync("INV", dto.InvoiceDate.Year);
            }
        }
        return (await GetAsync(inv.Id))!;
    }

    public async Task<InvoiceReadDto> IssueAsync(string id, string? actor)
    {
        var inv = await _db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new KeyNotFoundException("Invoice not found.");
        if (inv.Status != InvoiceStatus.Draft)
            throw new InvalidOperationException($"Invoice is {inv.Status}; only Draft invoices can be issued.");

        // The invoice's own currency, resolved from the document rather than assumed, so the journal is
        // posted in what the customer was actually billed in.
        var invoiceCurrency = await _db.Currencies.FirstOrDefaultAsync(c => c.Id == inv.CurrencyId)
            ?? throw new InvalidOperationException($"Invoice {inv.InvoiceNo} refers to a currency that no longer exists.");

        // Build the balanced posting: Dr receivable (total) / Cr revenue per account + Cr VAT.
        var lines = new List<CreateJournalLineDto>
        {
            new() { AccountCode = inv.ReceivableAccountCode, Debit = inv.Total, Credit = 0,
                    Description = $"{inv.InvoiceNo} — {inv.CustomerName}" },
        };
        foreach (var g in inv.Lines.GroupBy(l => l.RevenueAccountCode))
            lines.Add(new CreateJournalLineDto { AccountCode = g.Key, Debit = 0, Credit = g.Sum(x => x.LineSubtotal) });
        if (inv.VatAmount > 0)
            lines.Add(new CreateJournalLineDto { AccountCode = VatAccount, Debit = 0, Credit = inv.VatAmount });

        var jrnl = await _journals.CreateAsync(new CreateJournalDto
        {
            EntryDate = inv.InvoiceDate,
            // The journal must be posted in the INVOICE's currency. Omitting this booked a USD 1,000
            // invoice as a KES 1,000 journal: the AR subledger said one thing, the general ledger another,
            // and the two disagreed by the exchange rate with nothing reconciling them (#226).
            CurrencyCode = invoiceCurrency.Code,
            Description = $"Sales invoice {inv.InvoiceNo} — {inv.CustomerName}",
            SourceModule = "Finance-AR", SourceDocumentId = inv.Id,
            PostImmediately = true, Lines = lines,
        }, actor);

        inv.JournalEntryId = jrnl.Id;
        inv.Status = InvoiceStatus.Issued;

        // eTIMS (stub).
        var (reference, accepted) = await _etims.SubmitInvoiceAsync(inv.InvoiceNo, inv.Total, inv.VatAmount);
        inv.EtimsReference = reference;
        inv.EtimsStatus = accepted ? EtimsStatus.Accepted : EtimsStatus.Rejected;
        _db.EtimsSubmissions.Add(new EtimsSubmission
        {
            InvoiceId = inv.Id, EtimsReference = reference,
            Status = inv.EtimsStatus, AcknowledgedAt = DateTime.UtcNow,
        });
        inv.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await GetAsync(inv.Id))!;
    }

    /// <summary>
    /// Cancel an invoice, reversing its GL posting if it had one.
    ///
    /// <para><b>Why cancel rather than delete.</b> An issued invoice must never vanish: the customer
    /// has a copy, the GL has a posting, and the AR subledger has a balance. Removing the row leaves
    /// the trial balance and the statement disagreeing with nothing to reconcile them. Cancelling
    /// states that the document existed and no longer stands, which is what an auditor needs to see.
    /// This is the reason <c>InvoiceStatus.Cancelled</c> exists — it was read by seven filters in
    /// this service and assigned by nothing at all until now (#332).</para>
    ///
    /// <para><b>Why the guard is on PaidAmount and not only on Status.</b> Money received against an
    /// invoice cannot be un-received by cancelling it — the receipt posted its own journal and sits in
    /// the bank. Status alone would be the tidier test, since ReceiptService sets Paid/PartPaid
    /// whenever it allocates. But that is one code path maintaining two facts, and the fact that
    /// matters here is the money. A second allocation path added later that forgets the status line
    /// would silently make paid invoices cancellable, and this guard is the one standing between that
    /// mistake and a reversed receivable with the cash still on the books.</para>
    /// </summary>
    public async Task<InvoiceReadDto> CancelAsync(string id, string reason, string? actor)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A cancellation reason is required.");

        var inv = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new KeyNotFoundException("Invoice not found.");

        if (inv.Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException($"Invoice {inv.InvoiceNo} is already cancelled.");

        // Checked before Status, because it is the stronger statement: if any money has been
        // allocated, cancelling is wrong regardless of what the status says.
        if (inv.PaidAmount > 0)
            throw new InvalidOperationException(
                $"Invoice {inv.InvoiceNo} has {inv.PaidAmount:N2} received against it and cannot be "
                + "cancelled. Reverse or refund the receipt first, or raise a credit note.");

        if (inv.Status is InvoiceStatus.Paid or InvoiceStatus.PartPaid)
            throw new InvalidOperationException(
                $"Invoice {inv.InvoiceNo} is {inv.Status} and cannot be cancelled.");

        // A Draft invoice never posted, so there is nothing to reverse — reversing here would look for
        // a journal that does not exist. An Issued one always has a journal (IssueAsync sets it in the
        // same SaveChanges as the status), but this reads the field rather than trusting the pairing,
        // because a null here would otherwise surface as a KeyNotFound from deep inside JournalService.
        if (inv.Status != InvoiceStatus.Draft)
        {
            if (string.IsNullOrEmpty(inv.JournalEntryId))
                throw new InvalidOperationException(
                    $"Invoice {inv.InvoiceNo} is {inv.Status} but has no journal recorded, so cancelling "
                    + "it would leave its GL posting in place. This needs investigation, not a cancel.");

            // Reverses into the current open period and refuses if that period is closed (see #342).
            // Deliberately NOT caught: a cancel that cannot reverse the posting must fail whole rather
            // than leave a Cancelled invoice whose revenue is still recognised.
            await _journals.ReverseAsync(inv.JournalEntryId, actor);
        }

        inv.Status = InvoiceStatus.Cancelled;
        inv.CancellationReason = reason.Trim();
        inv.CancelledAt = DateTime.UtcNow;
        // The customer owes nothing now. Left as a written fact rather than a computed one because
        // AgingAsync filters on Status while summing Balance, and a cancelled invoice still carrying a
        // balance would drop out of aging while remaining a non-zero receivable in any report that
        // sums Balance without repeating that filter.
        inv.Balance = 0m;
        inv.UpdatedAt = DateTime.UtcNow;
        inv.UpdatedBy = actor;

        // eTIMS: a cancelled invoice that was already accepted by KRA needs a credit note submitted
        // upstream, not a local status change. The provider is a stub (#224), so recording the state
        // is all that is honest here — flipping EtimsStatus to something like Cancelled would claim
        // KRA had been told, which it has not.
        if (inv.EtimsStatus == EtimsStatus.Accepted)
            inv.Notes = string.IsNullOrWhiteSpace(inv.Notes)
                ? "Cancelled after eTIMS acceptance — a credit note is still owed to KRA (#224)."
                : inv.Notes + " | Cancelled after eTIMS acceptance — a credit note is still owed to KRA (#224).";

        await _db.SaveChangesAsync();
        return (await GetAsync(inv.Id))!;
    }

    public async Task<InvoiceReadDto?> GetAsync(string id)
    {
        var inv = await _db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id);
        if (inv == null) return null;
        var cats = await _db.TaxCategories.ToDictionaryAsync(t => t.Id, t => t.Code);
        var currencies = await _db.Currencies.ToDictionaryAsync(c => c.Id, c => c.Code);
        return Map(inv, cats, currencies);
    }

    public async Task<List<InvoiceReadDto>> ListAsync(int limit = 200)
    {
        var invs = await _db.Invoices.Include(i => i.Lines)
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.InvoiceNo).Take(limit).ToListAsync();
        var cats = await _db.TaxCategories.ToDictionaryAsync(t => t.Id, t => t.Code);
        var currencies = await _db.Currencies.ToDictionaryAsync(c => c.Id, c => c.Code);
        return invs.Select(i => Map(i, cats, currencies)).ToList();
    }

    public async Task<List<DebtorAgingRowDto>> AgingAsync(DateTime asOf)
    {
        var open = await _db.Invoices
            .Where(i => i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled && i.Balance > 0)
            .ToListAsync();
        return open.GroupBy(i => new { i.CustomerId, i.CustomerName }).Select(g =>
        {
            var row = new DebtorAgingRowDto { CustomerId = g.Key.CustomerId, CustomerName = g.Key.CustomerName ?? "" };
            foreach (var i in g)
            {
                var days = (asOf.Date - i.DueDate.Date).Days;
                if (days <= 0) row.Current += i.Balance;
                else if (days <= 30) row.Days1To30 += i.Balance;
                else if (days <= 60) row.Days31To60 += i.Balance;
                else row.Days61Plus += i.Balance;
            }
            row.Total = row.Current + row.Days1To30 + row.Days31To60 + row.Days61Plus;
            return row;
        }).OrderByDescending(r => r.Total).ToList();
    }

    private static InvoiceReadDto Map(Invoice i, IReadOnlyDictionary<string, string> cats, IReadOnlyDictionary<string, string> currencies) => new()
    {
        Id = i.Id, InvoiceNo = i.InvoiceNo, CustomerId = i.CustomerId, CustomerName = i.CustomerName,
        InvoiceDate = i.InvoiceDate, DueDate = i.DueDate, Status = i.Status.ToString(),
        CurrencyCode = currencies.TryGetValue(i.CurrencyId, out var ccy) ? ccy : null,
        Subtotal = i.Subtotal, VatAmount = i.VatAmount, Total = i.Total, PaidAmount = i.PaidAmount, Balance = i.Balance,
        CancellationReason = i.CancellationReason, CancelledAt = i.CancelledAt,
        EtimsReference = i.EtimsReference, EtimsStatus = i.EtimsStatus.ToString(), JournalEntryId = i.JournalEntryId, Notes = i.Notes,
        Lines = i.Lines.OrderBy(l => l.LineNo).Select(l => new InvoiceLineReadDto
        {
            LineNo = l.LineNo, Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
            TaxCode = cats.TryGetValue(l.TaxCategoryId, out var c) ? c : null, RevenueAccountCode = l.RevenueAccountCode,
            LineSubtotal = l.LineSubtotal, VatAmount = l.VatAmount, LineTotal = l.LineTotal,
        }).ToList(),
    };

    private async Task<string> NextNoAsync(string prefix, int year)
    {
        var count = await _db.Invoices.IgnoreQueryFilters().CountAsync(i => i.InvoiceDate.Year == year);
        return $"{prefix}-{year}-{(count + 1):D4}";
    }
}
