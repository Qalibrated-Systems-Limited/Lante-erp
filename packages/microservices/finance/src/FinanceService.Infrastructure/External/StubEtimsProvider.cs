using FinanceService.Core.Interfaces;

namespace FinanceService.Infrastructure.External;

/// Placeholder KRA eTIMS adapter — always "accepts" and returns a synthetic reference so the AR
/// flow works end-to-end. The integrations owner replaces this with the real eTIMS API call.
public class StubEtimsProvider : IEtimsProvider
{
    public Task<(string reference, bool accepted)> SubmitInvoiceAsync(string invoiceNo, decimal total, decimal vat)
    {
        var reference = $"ETIMS-{invoiceNo}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        return Task.FromResult((reference, true));
    }
}
