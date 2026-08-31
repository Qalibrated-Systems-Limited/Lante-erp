namespace ProcurementService.Core.Enums;

/// <summary>P9 (PROC-002) — outcome of a biannual supplier performance review. Below 60 raises a warning;
/// below 40 escalates to the MD, who may then blacklist through the ASR (P1) — the review never blacklists
/// by itself, because blacklisting requires MD authority and a mandatory reason.</summary>
public enum ReviewOutcome
{
    /// <summary>No transaction data in the period, so nothing could be scored — deliberately NOT a failure.</summary>
    NotAssessed,
    Satisfactory,   // >= 60
    Warning,        // 40–59
    MdEscalation,   // < 40
}
