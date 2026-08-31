using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Services;

public class VatService : IVatService
{
    private readonly FinanceDbContext _db;
    private readonly ISupplierInvoiceService _ap;
    public VatService(FinanceDbContext db, ISupplierInvoiceService ap) { _db = db; _ap = ap; }

    public async Task<VatReturnDto> ComputeAsync(string periodId)
    {
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new InvalidOperationException("Period not found.");
        // Output VAT = VAT on issued invoices in the period; Input VAT = VAT on approved supplier invoices.
        var outputVat = await _db.Invoices
            .Where(i => i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled
                        && i.InvoiceDate >= period.StartDate && i.InvoiceDate <= period.EndDate)
            .SumAsync(i => (decimal?)i.VatAmount) ?? 0;
        var inputVat = await _ap.InputVatForPeriodAsync(period.StartDate, period.EndDate);
        return new VatReturnDto
        {
            PeriodId = period.Id, PeriodName = period.Name,
            OutputVat = outputVat, InputVat = inputVat, NetVatPayable = outputVat - inputVat,
            Status = (await _db.VatReturns.AnyAsync(v => v.PeriodId == periodId && v.Status == VatReturnStatus.Filed))
                ? "Filed" : "Draft",
        };
    }

    public async Task<VatReturnDto> FileAsync(string periodId, string? actor)
    {
        var computed = await ComputeAsync(periodId);
        var existing = await _db.VatReturns.FirstOrDefaultAsync(v => v.PeriodId == periodId);
        if (existing == null)
        {
            existing = new VatReturn { PeriodId = periodId };
            _db.VatReturns.Add(existing);
        }
        existing.OutputVat = computed.OutputVat;
        existing.InputVat = computed.InputVat;
        existing.NetVatPayable = computed.NetVatPayable;
        existing.Status = VatReturnStatus.Filed;
        existing.FiledAt = DateTime.UtcNow;
        existing.FiledBy = actor;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        computed.Status = "Filed";
        return computed;
    }
}
