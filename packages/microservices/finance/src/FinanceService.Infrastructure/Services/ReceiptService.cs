using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

public class ReceiptService : IReceiptService
{
    private readonly FinanceDbContext _db;
    private readonly IJournalService _journals;
    public ReceiptService(FinanceDbContext db, IJournalService journals) { _db = db; _journals = journals; }

    public async Task<ReceiptReadDto> CreateAsync(CreateReceiptDto dto, string? actor)
    {
        if (dto.Amount <= 0) throw new InvalidOperationException("Receipt amount must be positive.");
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == dto.CustomerId)
            ?? throw new InvalidOperationException("Customer not found.");
        var baseCcy = await _db.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency)
            ?? throw new InvalidOperationException("No base currency configured.");
        var ccy = dto.CurrencyCode == null ? baseCcy
            : await _db.Currencies.FirstOrDefaultAsync(c => c.Code == dto.CurrencyCode) ?? baseCcy;

        var pay = new Payment
        {
            PaymentNo = await NextNoAsync("RCP", dto.PaymentDate.Year),
            CustomerId = customer.Id, CustomerName = customer.Name, PaymentDate = dto.PaymentDate,
            Amount = dto.Amount, CurrencyId = ccy.Id,
            Channel = Enum.TryParse<PaymentChannel>(dto.Channel, true, out var ch) ? ch : PaymentChannel.Bank,
            ReceiptReference = dto.ReceiptReference, ReceivedBy = actor,
            DepositAccountCode = dto.DepositAccountCode ?? "1100", CreatedBy = actor,
        };

        // Determine allocations: explicit, else auto oldest-invoice-first.
        var allocations = dto.Allocations.Count > 0
            ? dto.Allocations.Where(a => a.Amount > 0).ToList()
            : await AutoAllocateAsync(customer.Id, dto.Amount);

        decimal allocated = 0;
        var receivableCode = customer.IsGovernment ? "1201" : "1200";
        foreach (var a in allocations)
        {
            var inv = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == a.InvoiceId);
            if (inv == null) continue;

            // AutoAllocateAsync already excludes Draft and Cancelled. This — the EXPLICIT allocation
            // path — did not, and it reaches exactly the same write. One filter, correct, applied to
            // one of the two paths into it (#212, same shape as #342's period lock).
            //
            // A Draft invoice has a Balance but no GL posting, so allocating to one banked the cash,
            // flipped the invoice to PartPaid, and credited the receivable control account with no
            // matching debit — measured at -500 on a 500 allocation. Worse, IssueAsync only accepts
            // Draft, so the invoice could never afterwards be issued and its revenue became
            // permanently unrecognisable.
            //
            // Thrown rather than skipped. `continue` is right for an id that no longer resolves, but
            // a Draft or Cancelled invoice is one the caller deliberately named; silently turning
            // their allocation into unallocated cash hides a mistake that has to be unpicked by hand.
            if (inv.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
                throw new InvalidOperationException(
                    $"Invoice {inv.InvoiceNo} is {inv.Status} and cannot take a payment. "
                    + (inv.Status == InvoiceStatus.Draft
                        ? "Issue it first — an unissued invoice has no posting for the receipt to settle."
                        : "A cancelled invoice is owed nothing; record this as unallocated or against another invoice."));

            var amt = Math.Min(a.Amount, inv.Balance);
            if (amt <= 0) continue;
            inv.PaidAmount += amt; inv.Balance -= amt;
            inv.Status = inv.Balance <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartPaid;
            inv.UpdatedAt = DateTime.UtcNow;
            pay.Allocations.Add(new PaymentAllocation { PaymentId = pay.Id, InvoiceId = inv.Id, Amount = amt });
            allocated += amt;
        }
        pay.UnallocatedAmount = dto.Amount - allocated;
        _db.Payments.Add(pay);

        // PaymentNo is unique-indexed; retry with a freshly counted number on collision (see
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
                pay.PaymentNo = await NextNoAsync("RCP", dto.PaymentDate.Year);
            }
        }

        // Post: Dr deposit account / Cr receivable (full amount received).
        var jrnl = await _journals.CreateAsync(new CreateJournalDto
        {
            EntryDate = dto.PaymentDate,
            Description = $"Receipt {pay.PaymentNo} — {customer.Name}",
            SourceModule = "Finance-AR", SourceDocumentId = pay.Id, PostImmediately = true,
            Lines = new()
            {
                new() { AccountCode = pay.DepositAccountCode, Debit = dto.Amount, Credit = 0 },
                new() { AccountCode = receivableCode, Debit = 0, Credit = dto.Amount },
            },
        }, actor);
        pay.JournalEntryId = jrnl.Id;
        await _db.SaveChangesAsync();

        return Map(pay, await CurrencyCodesAsync());
    }

    public async Task<List<ReceiptReadDto>> ListAsync(int limit = 200)
    {
        var pays = await _db.Payments.Include(p => p.Allocations)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.PaymentNo).Take(limit).ToListAsync();
        var ccy = await CurrencyCodesAsync();
        return pays.Select(p => Map(p, ccy)).ToList();
    }

    private async Task<List<AllocationDto>> AutoAllocateAsync(string customerId, decimal amount)
    {
        var open = await _db.Invoices
            .Where(i => i.CustomerId == customerId && i.Balance > 0
                        && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled)
            .OrderBy(i => i.DueDate).ToListAsync();
        var list = new List<AllocationDto>(); var remaining = amount;
        foreach (var i in open)
        {
            if (remaining <= 0) break;
            var amt = Math.Min(remaining, i.Balance);
            list.Add(new AllocationDto { InvoiceId = i.Id, Amount = amt });
            remaining -= amt;
        }
        return list;
    }


    /// <summary>
    /// Currency id → ISO code, for projecting the transaction currency onto read DTOs. One query per
    /// request rather than a join per row: the table has a handful of rows and is static within a request.
    /// </summary>
    private async Task<Dictionary<string, string>> CurrencyCodesAsync() =>
        await _db.Currencies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code);

    private static ReceiptReadDto Map(Payment p, IReadOnlyDictionary<string, string> ccy) => new()
    {
        CurrencyCode = ccy.TryGetValue(p.CurrencyId, out var code) ? code : null,
        Id = p.Id, PaymentNo = p.PaymentNo, CustomerId = p.CustomerId, CustomerName = p.CustomerName,
        PaymentDate = p.PaymentDate, Amount = p.Amount, Channel = p.Channel.ToString(),
        ReceiptReference = p.ReceiptReference, UnallocatedAmount = p.UnallocatedAmount, JournalEntryId = p.JournalEntryId,
        Allocations = p.Allocations.Select(a => new AllocationDto { InvoiceId = a.InvoiceId, Amount = a.Amount }).ToList(),
    };

    private async Task<string> NextNoAsync(string prefix, int year)
    {
        var count = await _db.Payments.IgnoreQueryFilters().CountAsync(p => p.PaymentDate.Year == year);
        return $"{prefix}-{year}-{(count + 1):D4}";
    }
}
