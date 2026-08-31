namespace ProcurementService.Core.Enums;

/// <summary>P3 — QSL threshold-based sourcing band, derived from the PR total. Sets the minimum number of
/// written quotes required before an LPO may be raised (PROC / QSL Procurement Policy).</summary>
public enum SourcingBand
{
    DirectLpo,      // ≤ 10,000 — no quote required
    OneQuote,       // 10,001 – 100,000
    TwoQuotes,      // 100,001 – 500,000
    ThreeQuotesMd,  // > 500,000 — 3 quotes + MD approval (Board at P4)
}

/// <summary>P3 — comparative-analysis lifecycle. Completing the comparison records the recommended supplier
/// and unblocks P4 LPO generation.</summary>
public enum ComparisonStatus
{
    Draft,
    Completed,
}
