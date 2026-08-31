using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Infrastructure.Services;

/// <summary>Config-gated no-op Finance seam (Finance:Enabled). Logs the intended invoice and reports
/// success without an id until the real finance-service HTTP client is wired (O10).</summary>
public class NoOpFinanceGateway(IConfiguration config, ILogger<NoOpFinanceGateway> logger) : IFinanceGateway
{
    public Task<FinanceInvoiceResult> RaiseDealInvoiceAsync(DealInvoiceRequest request, CancellationToken ct = default)
    {
        var enabled = config.GetValue<bool>("Finance:Enabled");
        logger.LogInformation("[Finance seam{State}] deal {Deal} → invoice {Amount} {Cur} for {Customer}",
            enabled ? "" : " (disabled)", request.DealNumber, request.Amount, request.Currency, request.CustomerName);
        return Task.FromResult(new FinanceInvoiceResult(true, null,
            enabled ? "Finance invoice requested." : "Finance integration disabled — invoice not created."));
    }
}
