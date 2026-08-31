using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>
/// P1 — Customer master record (CRM-001/004/005/006/051/057/058). The system-of-record for external
/// clients being sold to (distinct from the platform tenant/COMPANY). Enters via a 4-stage onboarding
/// approval chain; one exclusive Account Owner from first contact to close-out; introduced_by is
/// locked after MD approval to protect referral commission entitlements.
/// </summary>
public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; } = CustomerType.Company;
    public string? Industry { get; set; }
    public string? Segment { get; set; }
    public AccountTier AccountTier { get; set; } = AccountTier.Standard;
    public string? Geography { get; set; }
    public string? BusinessLine { get; set; }

    // Identity / dedup surface (duplicate check on name + email + phone before the approval chain).
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ClientReference { get; set; }   // external / legacy reference

    /// <summary>
    /// KRA PIN (Kenya Revenue Authority). Required on a tax invoice, so it is captured at
    /// onboarding. Stored upper-cased and treated as an identity key: two clients cannot share a
    /// PIN, which makes it the strongest duplicate signal available — far better than name.
    /// </summary>
    public string? KraPin { get; set; }

    // Ownership & attribution.
    public string AccountOwnerId { get; set; } = string.Empty;   // exclusive owner (CRM-051)
    public string? AccountOwnerName { get; set; }
    public string IntroducedBy { get; set; } = string.Empty;     // locked after MD approval (CRM-058)
    public bool IntroducedByLocked { get; set; }

    // Credit (set by CFO at onboarding — CRM-005).
    public decimal CreditLimit { get; set; }
    public int CreditTermsDays { get; set; }

    // Onboarding workflow.
    public CustomerStatus Status { get; set; } = CustomerStatus.PendingLineManager;
    public string SubmittedBy { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string? LineManagerApprovedBy { get; set; }
    public DateTime? LineManagerApprovedAt { get; set; }
    public string? HeadBdApprovedBy { get; set; }
    public DateTime? HeadBdApprovedAt { get; set; }
    public string? CfoApprovedBy { get; set; }
    public DateTime? CfoApprovedAt { get; set; }
    public string? MdApprovedBy { get; set; }
    public DateTime? MdApprovedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    // P7 — dormancy tracking (no interaction/purchase in 90 days).
    public DateTime? LastInteractionAt { get; set; }
    public DateTime? DormantSince { get; set; }

    // P12 — after-sales latest scores (denormalised from the survey tables for the account view).
    public decimal? LastSatisfactionScore { get; set; }
    public int? LastNpsScore { get; set; }

    public ICollection<CustomerContact> Contacts { get; set; } = new List<CustomerContact>();
}
