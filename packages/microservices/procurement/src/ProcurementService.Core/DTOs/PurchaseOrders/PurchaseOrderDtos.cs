namespace ProcurementService.Core.DTOs.PurchaseOrders;

// ── Generate ──
public class GenerateLpoDto
{
    /// <summary>Required only for the direct-LPO band (≤10k) where there is no quotation comparison to
    /// pick a supplier from; otherwise the comparison's recommended supplier is used.</summary>
    public string? SupplierId { get; set; }
    public decimal? TotalAmount { get; set; }   // optional override (else from recommended quote / PR total)
    /// <summary>Date the supplier commits to deliver by — recorded so the P9 delivery score can measure a
    /// true on-time percentage.</summary>
    public DateTime? PromisedDeliveryDate { get; set; }
}

// ── Reads ──
public class PoApprovalDto
{
    public string Id { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public bool Signed { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? Notes { get; set; }
}

public class PoReadDto
{
    public string Id { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string PrId { get; set; } = string.Empty;
    public string? QuotationComparisonId { get; set; }
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "KES";
    public string Band { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool BoardResolutionRequired { get; set; }
    public string? BoardResolutionRef { get; set; }
    public string? BoardResolutionUrl { get; set; }
    /// <summary>DEC-C — false means the resolution was captured locally without a Compliance check.</summary>
    public bool BoardResolutionVerified { get; set; }
    public string? BoardResolutionId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string ReceiptStatus { get; set; } = "NotReceived";
    public DateTime? ReceivedAt { get; set; }
    public string? LastGrnRef { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal RejectedQty { get; set; }
    public DateTime? PromisedDeliveryDate { get; set; }
    // P8 — emergency purchases: sourcing waived, MD authorisation captured before the purchase.
    public bool IsEmergency { get; set; }
    public string? EmergencyReason { get; set; }
    public bool QuotationWaiver { get; set; }
    public string? EmergencyApprovedBy { get; set; }
    public DateTime? EmergencyApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PoApprovalDto> Approvals { get; set; } = new();
    public string? NextApprovalRole { get; set; }   // the role of the next pending step, if any
}

public class PoSummaryRowDto
{
    public string Id { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public decimal TotalAmount { get; set; }
    public string Band { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ReceiptStatus { get; set; } = "NotReceived";
    public bool BoardResolutionRequired { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PoFilterParams
{
    public string? Status { get; set; }
    public string? SupplierId { get; set; }
    public string? PrId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record PoListResult(List<PoSummaryRowDto> Items, int Total);

// ── Actions ──
public class SignLpoDto
{
    public bool Approve { get; set; }
    public string? Notes { get; set; }
    public string? Reason { get; set; }   // required on reject
}

public class BoardResolutionDto
{
    public string ResolutionRef { get; set; } = string.Empty;
    public string? Url { get; set; }
}

public class PoSummaryDto
{
    public int Total { get; set; }
    public int PendingApproval { get; set; }
    public int Issued { get; set; }
    public int Rejected { get; set; }
    public int AwaitingBoardResolution { get; set; }
    public decimal IssuedValue { get; set; }
}

public record PoActionResult(string Status, string Message);

/// <summary>P5 — goods-receipt notice posted by the Stores GRN callback.</summary>
public class RecordReceiptDto
{
    public decimal AcceptedQty { get; set; }
    public decimal RejectedQty { get; set; }
    public bool PartialDelivery { get; set; }
    public string? GrnId { get; set; }
}
