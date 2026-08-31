using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>P2 — PR_APPROVAL_LOG. The permanent audit trail of every PR approval action (submit, approve,
/// reject, SLA escalation), so every downstream LPO can be traced back to its originating request.</summary>
public class PrApprovalLog : BaseEntity
{
    public string PrId { get; set; } = string.Empty;
    public string ApproverId { get; set; } = string.Empty;
    public string? ApproverName { get; set; }
    public PrAction Action { get; set; }
    public string? Reason { get; set; }
    public DateTime ActionedAt { get; set; } = DateTime.UtcNow;
}
