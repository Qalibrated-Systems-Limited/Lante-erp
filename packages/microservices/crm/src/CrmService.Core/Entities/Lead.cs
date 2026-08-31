using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>
/// P2 — Lead capture &amp; qualification (CRM-008, 036, MKT-005, TRK-003). Every lead records a source
/// at capture and is assigned to a Sales Engineer. A daily sweep flags leads with no activity in >2
/// days. Qualified leads convert to an opportunity (P3), setting IsConverted/ConvertedAt.
/// </summary>
public class Lead : BaseEntity
{
    public string? BranchId { get; set; }

    // Source (required at capture) + optional specific name (e.g. which exhibition / referrer).
    public LeadSourceType Source { get; set; } = LeadSourceType.Web;
    public string? SourceName { get; set; }
    public string? CampaignId { get; set; }   // marketing attribution (Campaign entity lands in C10)

    // Assigned Sales Engineer.
    public string AssignedTo { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }

    // Contact / company.
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Industry { get; set; }

    /// <summary>
    /// Which of the firm's service lines the lead is interested in, comma-separated. Captured at
    /// qualification so the lead can be routed to the right technical team and so marketing can
    /// report demand by line. Stored as text rather than a join table because there is no product
    /// catalogue entity in CRM yet; see ProductRanges for the accepted vocabulary.
    /// </summary>
    public string? ProductRange { get; set; }

    public decimal EstimatedValue { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public LeadRating Rating { get; set; } = LeadRating.Warm;
    public string? Notes { get; set; }

    // Activity / stale tracking (2-day no-activity sweep).
    public DateTime? LastActivityAt { get; set; }
    public DateTime? StaleAlertedAt { get; set; }

    // Conversion.
    public bool IsConverted { get; set; }
    public string? ConvertedCustomerId { get; set; }
    public string? ConvertedOpportunityId { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public string? UnqualifiedReason { get; set; }

    public ICollection<LeadActivity> Activities { get; set; } = new List<LeadActivity>();
}
