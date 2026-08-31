namespace CrmService.Core.Enums;

// P2 — lead lifecycle. Qualified leads convert to opportunities (P3), setting Status=Converted.
public enum LeadStatus
{
    New,
    Contacted,
    Qualified,
    Unqualified,
    Converted,
}

public enum LeadRating
{
    Cold,
    Warm,
    Hot,
}

// Source must be recorded at capture (CRM-008). The 6 standard channels + Other.
public enum LeadSourceType
{
    Web,
    Referral,
    Tender,
    ColdCall,
    Exhibition,
    Social,
    Other,
}
