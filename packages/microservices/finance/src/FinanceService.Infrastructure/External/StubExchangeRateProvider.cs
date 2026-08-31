using FinanceService.Core.Interfaces;

namespace FinanceService.Infrastructure.External;

/// Placeholder FX provider. Returns indicative fixed rates so the module works end-to-end; the
/// integrations owner replaces this with the real CBK / Open-FX adapter behind IExchangeRateProvider.
public class StubExchangeRateProvider : IExchangeRateProvider
{
    private static readonly Dictionary<string, decimal> Indicative = new(StringComparer.OrdinalIgnoreCase)
    {
        ["KES"] = 1m, ["USD"] = 129.50m, ["CNY"] = 17.80m, ["EUR"] = 140.20m, ["GBP"] = 164.10m,
    };

    public Task<IReadOnlyDictionary<string, decimal>> FetchRatesAsync(string baseCode, IEnumerable<string> targetCodes)
    {
        // A currency this provider does not know is OMITTED, never defaulted to 1.
        //
        // It previously fell back to `1m`, which meant an unrecognised currency was silently
        // treated as at par with the Kenyan shilling — add ZAR or UGX and every transaction in it
        // is wrong by two orders of magnitude, with nothing on screen to indicate it. Parity is
        // never a safe default for a currency you cannot price; the caller must see the absence.
        //
        // Callers therefore have to handle a missing key. LedgerControllers already does:
        // `if (rates.TryGetValue(c.Code, out var r))` leaves the stored rate untouched when the
        // provider has nothing, so an unknown currency keeps whatever a human last set rather
        // than being silently reset.
        IReadOnlyDictionary<string, decimal> result = targetCodes
            .Where(c => Indicative.ContainsKey(c))
            .ToDictionary(c => c, c => Indicative[c], StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(result);
    }
}
