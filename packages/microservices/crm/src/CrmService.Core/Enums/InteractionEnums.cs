namespace CrmService.Core.Enums;

// P7 — client interaction & activity scheduling.
public enum InteractionType
{
    Call,
    Email,
    Meeting,
    SiteVisit,
    Proposal,
    Other,
}

public enum ActivityTaskStatus
{
    Open,
    Done,
    Cancelled,
}
