using FinanceService.Core.Enums;

namespace FinanceService.Core.Entities;

/// Process 1 — Currency & exchange rate setup. Base currency is KES; USD/CNY are transactional.
public class Currency : BaseEntity
{
    public string Code { get; set; } = string.Empty;           // ISO code: KES, USD, CNY
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public bool IsBaseCurrency { get; set; }
    /// <summary>Base-currency units per 1 unit of THIS currency (USD 131.25 = KES 131.25 per USD 1; the base
    /// currency itself is 1). Convert foreign → base by MULTIPLYING by this rate.</summary>
    public decimal ExchangeRate { get; set; } = 1m;
    public CurrencySource Source { get; set; } = CurrencySource.Manual;
    public DateTime? LastFetchedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

/// Forex gain/loss written when open multi-currency balances are revalued on a rate change.
public class ForexRevaluationLog : BaseEntity
{
    public string CurrencyId { get; set; } = string.Empty;
    public string? PeriodId { get; set; }
    public decimal OldRate { get; set; }
    public decimal NewRate { get; set; }
    public decimal GainLossAmount { get; set; }
    public string? JournalEntryId { get; set; }
    public DateTime RevaluedAt { get; set; } = DateTime.UtcNow;
}
