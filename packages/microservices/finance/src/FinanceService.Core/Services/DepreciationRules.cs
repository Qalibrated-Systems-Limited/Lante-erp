namespace FinanceService.Core.Services;

/// <summary>
/// The arithmetic behind a monthly depreciation charge.
///
/// <para>Extracted from <c>DepreciationService</c> so it can be tested (#230). The service itself
/// runs inside a transaction holding a <c>pg_advisory_xact_lock</c>, which neither the in-memory nor
/// the SQLite provider can honour — that is why it had no tests at all. The concurrency guard needs a
/// real Postgres fixture; the arithmetic does not, and the arithmetic is what silently produces a
/// wrong number on the balance sheet.</para>
/// </summary>
public static class DepreciationRules
{
    /// <summary>
    /// One month's charge for an asset.
    /// </summary>
    /// <param name="annualRate">
    /// A <b>FRACTION</b>, not a percentage — 0.20 means 20% a year. The seeded categories use this
    /// convention (0.3333 for IT equipment, 0.125 for furniture) and the formula divides by 12
    /// without dividing by 100.
    /// <para>Worth stating loudly because HR stores its statutory rates the other way round: NSSF is
    /// <c>6m</c> meaning 6%, and that engine divides by 100. Two conventions for "a rate" in one
    /// codebase, so seeding <c>20</c> here for "20%" would depreciate an asset a hundred times too
    /// fast, and the first sign of it would be a wrecked balance sheet.</para>
    /// </param>
    /// <param name="acquisitionCost">What the asset cost. Depreciation is on cost, not on NBV.</param>
    /// <param name="accumulatedDepreciation">What has already been charged against it.</param>
    /// <returns>The charge, never negative, never more than the remaining net book value.</returns>
    public static decimal MonthlyCharge(decimal annualRate, decimal acquisitionCost, decimal accumulatedDepreciation)
    {
        var monthly = Math.Round(annualRate * acquisitionCost / 12, 2);
        var remaining = acquisitionCost - accumulatedDepreciation;
        // Capped at what is left, so the final month charges the stub rather than taking the asset
        // below zero, and a fully depreciated asset charges nothing at all. Math.Max guards the
        // already-over-depreciated case, which a manual adjustment can produce.
        return Math.Min(monthly, Math.Max(remaining, 0m));
    }

    /// <summary>
    /// The posting date for a period: its last day, in UTC.
    /// </summary>
    /// <param name="period">yyyy-MM, e.g. 2026-07.</param>
    public static DateTime LastDayOfPeriod(string period)
    {
        var parts = (period ?? string.Empty).Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var month)
            || month < 1 || month > 12 || year < 1)
            throw new InvalidOperationException("Period must be in yyyy-MM format, e.g. 2026-07.");
        return new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
    }
}
