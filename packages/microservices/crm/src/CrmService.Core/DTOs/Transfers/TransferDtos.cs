namespace CrmService.Core.DTOs.Transfers;

public class RaiseTransferDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string IncomingOwnerId { get; set; } = string.Empty;
    public string? IncomingOwnerName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime? EffectiveDate { get; set; }
}

public class RejectTransferDto { public string Reason { get; set; } = string.Empty; }

public class UpdateHandoverDto
{
    public string? ClientHistory { get; set; }
    public string? ActiveWork { get; set; }
    public string? PricingNotes { get; set; }
    public string? CreditTerms { get; set; }
    public string? OpenIssues { get; set; }
    public string? HandoverDocUrl { get; set; }
}

// role: Outgoing | Incoming | DeptHead | Md
public class SignHandoverDto { public string Role { get; set; } = string.Empty; public string SignatoryName { get; set; } = string.Empty; }

public class HandoverDto
{
    public string Id { get; set; } = string.Empty;
    public string? ClientHistory { get; set; }
    public string? ActiveWork { get; set; }
    public string? PricingNotes { get; set; }
    public string? CreditTerms { get; set; }
    public string? OpenIssues { get; set; }
    public string? HandoverDocUrl { get; set; }
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
    public List<string> MissingSignatures { get; set; } = new();
}

public class TransferSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string OutgoingOwnerId { get; set; } = string.Empty;
    public string IncomingOwnerId { get; set; } = string.Empty;
    public string? IncomingOwnerName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? EffectiveDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TransferDetailDto : TransferSummaryDto
{
    public string Reason { get; set; } = string.Empty;
    public string RaisedBy { get; set; } = string.Empty;
    public string? HeadBdApprovedBy { get; set; }
    public DateTime? HeadBdApprovedAt { get; set; }
    public string? CfoApprovedBy { get; set; }
    public DateTime? CfoApprovedAt { get; set; }
    public string? MdApprovedBy { get; set; }
    public DateTime? MdApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public HandoverDto? Handover { get; set; }
}

public class TransferFilterParams
{
    public string? Status { get; set; }
    public string? CustomerId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record TransferListResult(List<TransferSummaryDto> Items, int Total);
public record TransferActionResult(string Status, string Message);
