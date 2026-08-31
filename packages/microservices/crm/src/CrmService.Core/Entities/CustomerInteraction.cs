using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P7 — CUSTOMER_INTERACTION. Every client touchpoint, chronologically. Logging one refreshes
/// the customer's LastInteractionAt and clears any dormant flag.</summary>
public class CustomerInteraction : BaseEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string? ContactId { get; set; }
    public InteractionType InteractionType { get; set; } = InteractionType.Call;
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Outcome { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime InteractionDate { get; set; } = DateTime.UtcNow;
    public DateTime? NextActionDate { get; set; }
}
