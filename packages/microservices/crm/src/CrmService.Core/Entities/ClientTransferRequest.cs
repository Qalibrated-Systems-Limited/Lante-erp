using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P8 — CLIENT_TRANSFER_REQUEST (CRM-051/055/056). 3-stage approval chain (Head of BD → CFO →
/// MD) to transfer a client's exclusive Account Owner.</summary>
public class ClientTransferRequest : BaseEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string OutgoingOwnerId { get; set; } = string.Empty;
    public string IncomingOwnerId { get; set; } = string.Empty;
    public string? IncomingOwnerName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime? EffectiveDate { get; set; }

    public TransferStatus Status { get; set; } = TransferStatus.PendingHeadBd;
    public string RaisedBy { get; set; } = string.Empty;

    public string? HeadBdApprovedBy { get; set; }
    public DateTime? HeadBdApprovedAt { get; set; }
    public string? CfoApprovedBy { get; set; }
    public DateTime? CfoApprovedAt { get; set; }
    public string? MdApprovedBy { get; set; }
    public DateTime? MdApprovedAt { get; set; }
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ClientTransferHandover? Handover { get; set; }
}
