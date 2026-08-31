using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// Polymorphic (ParentType + ParentId) investigation-step log shared across
// WhistleblowerCase, DataSubjectRequest and DataBreach — all three share the same
// submission-to-investigation-to-outcome lifecycle. Matches the ERD design note.
public class CaseAction : BaseEntity
{
    public CaseActionParentType ParentType { get; set; }
    public string ParentId { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string? LoggedByUserId { get; set; }
    public string? LoggedByName { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
}
