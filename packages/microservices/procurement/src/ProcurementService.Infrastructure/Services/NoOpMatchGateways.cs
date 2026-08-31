using Microsoft.Extensions.Logging;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Infrastructure.Services;

/// <summary>Inert payables read seam for deployments without Finance. Reports "not reachable" so the
/// matching engine fails closed rather than concluding no invoice exists.</summary>
public class NoOpInvoiceGateway(ILogger<NoOpInvoiceGateway> logger) : IInvoiceGateway
{
    public Task<InvoiceLookup> FindByLpoAsync(string poNumber, CancellationToken ct = default)
    {
        logger.LogInformation("Finance invoice lookup skipped for {Po} — no Finance gateway wired.", poNumber);
        return Task.FromResult(new InvoiceLookup(false, null,
            "Finance is not wired, so the supplier invoice cannot be verified."));
    }

    public Task<bool> SetMatchStatusAsync(string supplierInvoiceId, string status, string? note, CancellationToken ct = default)
        => Task.FromResult(false);
}

/// <summary>Inert payment handoff for deployments without Finance. Never reports a voucher as raised.</summary>
public class NoOpPaymentVoucherGateway(ILogger<NoOpPaymentVoucherGateway> logger) : IPaymentVoucherGateway
{
    public Task<VoucherResult> RaiseAsync(string supplierInvoiceId, decimal? amount, string? bankAccountCode, string poNumber, CancellationToken ct = default)
    {
        logger.LogWarning("Payment voucher for {Po} not raised — no Finance gateway wired.", poNumber);
        return Task.FromResult(new VoucherResult(false, null, null,
            "Finance is not wired, so no payment voucher can be raised."));
    }

    public Task<VoucherResult> RaiseAdHocAsync(string payee, decimal amountKes, string reference, CancellationToken ct = default)
    {
        logger.LogWarning("Advance voucher for {Ref} not raised — no Finance gateway wired.", reference);
        return Task.FromResult(new VoucherResult(false, null, null,
            "Finance is not wired, so no advance voucher can be raised."));
    }
}
