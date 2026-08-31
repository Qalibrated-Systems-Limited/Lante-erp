namespace CrmService.Core.Enums;

// P4 — quotation lifecycle. SE drafts → Dept Head reviews → MD approves (only if total > threshold)
// → Sent → Accepted/Rejected/Expired. Revising creates a new version; the prior one is Superseded
// (never deleted — full version control, CRM-016).
public enum QuotationStatus
{
    Draft,
    PendingDeptHead,
    PendingMd,
    Approved,
    Sent,
    Accepted,
    Rejected,
    Expired,
    Superseded,
}
