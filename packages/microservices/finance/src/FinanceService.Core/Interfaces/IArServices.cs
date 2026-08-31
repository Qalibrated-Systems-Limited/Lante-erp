using FinanceService.Core.DTOs;

namespace FinanceService.Core.Interfaces;

public interface IInvoiceService
{
    Task<InvoiceReadDto> CreateAsync(CreateInvoiceDto dto, string? actor);
    /// Issue: post to GL (Dr receivable / Cr revenue + VAT) and submit to eTIMS.
    Task<InvoiceReadDto> IssueAsync(string id, string? actor);
    /// Cancel: refuse if any money was received, reverse the GL posting if there was one.
    Task<InvoiceReadDto> CancelAsync(string id, string reason, string? actor);
    Task<InvoiceReadDto?> GetAsync(string id);
    Task<List<InvoiceReadDto>> ListAsync(int limit = 200);
    Task<List<DebtorAgingRowDto>> AgingAsync(DateTime asOf);
}

public interface IReceiptService
{
    Task<ReceiptReadDto> CreateAsync(CreateReceiptDto dto, string? actor);
    Task<List<ReceiptReadDto>> ListAsync(int limit = 200);
}

public interface IVatService
{
    Task<VatReturnDto> ComputeAsync(string periodId);
    Task<VatReturnDto> FileAsync(string periodId, string? actor);
}

/// Stubbed KRA eTIMS provider. The integrations owner swaps in the real adapter later.
public interface IEtimsProvider
{
    Task<(string reference, bool accepted)> SubmitInvoiceAsync(string invoiceNo, decimal total, decimal vat);
}
