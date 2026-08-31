using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>P4 — one step in an LPO's approval chain (per the value-based authority matrix). Signing a step
/// applies a digital signature (SignatureRef); the chain approves in Sequence order.</summary>
public class PoApproval : BaseEntity
{
    public string PoId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public ApprovalRole Role { get; set; }
    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;
    public string? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    /// <summary>Digital signature token applied when the step is signed (ARCH-007C).</summary>
    public string? SignatureRef { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? Notes { get; set; }

    public PurchaseOrder? Po { get; set; }
}
