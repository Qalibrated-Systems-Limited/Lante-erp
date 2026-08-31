using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>
/// P7 — LANDED_COST_COMPONENT. One cost incurred on the way from the foreign supplier to the store, held in
/// the currency it was billed in plus its KES equivalent at the rate prevailing on <see cref="IncurredOn"/>
/// (PROC-003 requires per-date rates, which is why the rate lives here and not only on the order).
/// <para>Components sourced from the customs declaration (import duty, clearing, port charges) are created
/// and refreshed from it, so the landed cost sums components only and never double-counts customs.</para>
/// </summary>
public class LandedCostComponent : BaseEntity
{
    public string IntlPoId { get; set; } = string.Empty;
    public LandedCostComponentType ComponentType { get; set; }

    public decimal AmountFx { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    /// <summary>KES per 1 unit of <see cref="CurrencyCode"/> on the date this cost was incurred.</summary>
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal KesAmount { get; set; }

    public DateTime IncurredOn { get; set; } = DateTime.UtcNow;
    public string? BlNumber { get; set; }
    public DateTime? Eta { get; set; }
    public string? Notes { get; set; }

    /// <summary>True when this row is derived from the customs declaration rather than entered directly —
    /// such rows are replaced, not duplicated, when the declaration is re-lodged.</summary>
    public bool FromCustoms { get; set; }

    public InternationalPo? IntlPo { get; set; }
}
