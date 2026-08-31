using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// COMP-003: anonymous submission portal; restricted access (own hse.approve-style
// permission pair, "compliance.whistleblower.*", kept deliberately separate from the
// general compliance.read/write policy so ordinary compliance staff can't see these).
public class WhistleblowerCase : BaseEntity
{
    public string RefNo { get; set; } = string.Empty;
    public bool Anonymous { get; set; }
    public string? SubmittedByUserId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public WhistleblowerStatus Status { get; set; } = WhistleblowerStatus.New;
    public string? Outcome { get; set; }
}
