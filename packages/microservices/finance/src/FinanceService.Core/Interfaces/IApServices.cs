using FinanceService.Core.DTOs;
using FinanceService.Core.Enums;

namespace FinanceService.Core.Interfaces;

public interface ISupplierInvoiceService
{
    Task<SupplierInvoiceReadDto> CreateAsync(CreateSupplierInvoiceDto dto, string? actor);
    /// Approve: post Dr expense (per line) + Dr input VAT (1230) / Cr payables (2100).
    Task<SupplierInvoiceReadDto> ApproveAsync(string id, string? actor);
    /// Cancel: refuse if any money was paid, reverse the GL posting if there was one.
    Task<SupplierInvoiceReadDto> CancelAsync(string id, string reason, string? actor);
    Task<SupplierInvoiceReadDto?> GetAsync(string id);
    Task<List<SupplierInvoiceReadDto>> ListAsync(int limit = 200);
    /// Record the 3-way match outcome Procurement owns (Finance only stores it, and refuses to approve an
    /// invoice whose match is in Exception). Called by the Procurement write-back seam.
    Task<SupplierInvoiceReadDto> SetMatchStatusAsync(string id, ThreeWayMatchStatus status, string? note, string? actor);
    /// Total input VAT on approved supplier invoices dated within the period (for the VAT return).
    Task<decimal> InputVatForPeriodAsync(DateTime start, DateTime end);
}

public interface IPaymentVoucherService
{
    Task<VoucherReadDto> CreateAsync(CreateVoucherDto dto, string? actor);
    Task<VoucherReadDto> ApproveAsync(string id, string? actor, ApprovalContext? ctx = null);
    Task<VoucherReadDto> PayAsync(string id, string? actor);
    Task<List<VoucherReadDto>> ListAsync(int limit = 200);
    /// The role required to approve a payment of this amount (payment authority matrix).
    Task<string> RequiredAuthorityAsync(decimal amount);
}
