namespace CrmService.Core.Enums;

// Onboarding + lifecycle status. New clients enter a 4-stage approval chain (P1, CRM-057) before
// they become part of the master database: LineManager → Head of BD → CFO → MD.
public enum CustomerStatus
{
    PendingLineManager,   // submitted, awaiting line-manager approval
    PendingHeadBd,        // awaiting Head of Business Development
    PendingCfo,           // awaiting CFO credit-terms review
    PendingMd,            // awaiting MD final approval
    Active,               // approved — in the master database
    Rejected,             // rejected at some stage (reason recorded)
    Inactive,             // deactivated after being active
}

public enum AccountTier
{
    Standard,
    Silver,
    Gold,
    Platinum,
}

public enum CustomerType
{
    Company,
    Individual,
    Government,
    Ngo,
    Other,
}
