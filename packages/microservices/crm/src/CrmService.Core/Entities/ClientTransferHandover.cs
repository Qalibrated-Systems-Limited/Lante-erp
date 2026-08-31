namespace CrmService.Core.Entities;

/// <summary>P8 — CLIENT_TRANSFER_HANDOVER (CRM-056). Status Handover Document covering client history,
/// active work, pricing, credit terms &amp; open issues; signed by all 4 parties (outgoing owner,
/// incoming owner, Department Head, MD). Permanent once completed — cannot be deleted.</summary>
public class ClientTransferHandover : BaseEntity
{
    public string TransferRequestId { get; set; } = string.Empty;
    public string? ClientHistory { get; set; }
    public string? ActiveWork { get; set; }
    public string? PricingNotes { get; set; }
    public string? CreditTerms { get; set; }
    public string? OpenIssues { get; set; }
    public string? HandoverDocUrl { get; set; }

    // Four mandatory signatures.
    public string? OutgoingSignedName { get; set; }
    public DateTime? OutgoingSignedAt { get; set; }
    public string? IncomingSignedName { get; set; }
    public DateTime? IncomingSignedAt { get; set; }
    public string? DeptHeadSignedName { get; set; }
    public DateTime? DeptHeadSignedAt { get; set; }
    public string? MdSignedName { get; set; }
    public DateTime? MdSignedAt { get; set; }

    public bool IsPermanent { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ClientTransferRequest? TransferRequest { get; set; }
}
