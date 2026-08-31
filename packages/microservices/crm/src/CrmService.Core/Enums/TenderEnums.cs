namespace CrmService.Core.Enums;

// P5 — tender lifecycle. Won tenders link to an opportunity (→ P3/P6); Lost records a reason.
public enum TenderStatus
{
    Registered,
    Submitted,
    Won,
    Lost,
    NoBid,
    Cancelled,
}

public enum BidBondStatus
{
    Active,
    Released,
    Expired,
    Forfeited,
}
