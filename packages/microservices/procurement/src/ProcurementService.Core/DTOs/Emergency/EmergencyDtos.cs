namespace ProcurementService.Core.DTOs.Emergency;

// ── Declaration ──
public class DeclareEmergencyDto
{
    public string SupplierId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    /// <summary>Mandatory written justification (PROC-004).</summary>
    public string EmergencyReason { get; set; } = string.Empty;
    /// <summary>Mandatory quotation-waiver document.</summary>
    public string QuotationWaiverUrl { get; set; } = string.Empty;
    public string WaiverReason { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class MdApproveEmergencyDto
{
    /// <summary>Mandatory reference for the MD's authorisation (minute, email or signed note).</summary>
    public string MdApprovalRef { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>The post-hoc requisition that must follow within 24 hours. Lines are optional — omitted, a single
/// line is synthesised from the LPO value so the requisition still balances to the purchase.</summary>
public class PostHocPrDto
{
    public string? DepartmentId { get; set; }
    public string? Justification { get; set; }
    public List<PostHocLineDto> Lines { get; set; } = new();
}

public class PostHocLineDto
{
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public string? Unit { get; set; }
    public decimal EstimatedUnitPrice { get; set; }
}

public class BoardPackDto
{
    /// <summary>Reporting period, "YYYY-MM".</summary>
    public string Period { get; set; } = string.Empty;
}

// ── Reads ──
public class EmergencyReadDto
{
    public string Id { get; set; } = string.Empty;
    public string PoId { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public decimal TotalAmount { get; set; }
    public string PoStatus { get; set; } = string.Empty;
    public string ReceiptStatus { get; set; } = "NotReceived";

    public string DeclaredBy { get; set; } = string.Empty;
    public DateTime DeclaredAt { get; set; }
    public string EmergencyReason { get; set; } = string.Empty;

    public string? MdApprovalRef { get; set; }
    public string? MdApprovedBy { get; set; }
    public DateTime? MdApprovedAt { get; set; }

    public string QuotationWaiverUrl { get; set; } = string.Empty;
    public string WaiverReason { get; set; } = string.Empty;

    public DateTime PostHocDueAt { get; set; }
    public string? PostHocPrId { get; set; }
    public string? PostHocPrNumber { get; set; }
    public DateTime? PostHocRaisedAt { get; set; }
    /// <summary>No post-hoc requisition yet and the 24-hour window has passed.</summary>
    public bool PostHocOverdue { get; set; }
    /// <summary>Raised, but after the 24-hour window — a reportable breach rather than a blocker.</summary>
    public bool PostHocRaisedLate { get; set; }

    public string? BoardPackPeriod { get; set; }
    public DateTime? BoardPackMarkedAt { get; set; }

    public bool AwaitingMdApproval { get; set; }
    public bool BoardPackPending { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EmergencyRowDto
{
    public string Id { get; set; } = string.Empty;
    public string PoId { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public decimal TotalAmount { get; set; }
    public string PoStatus { get; set; } = string.Empty;
    public DateTime DeclaredAt { get; set; }
    public string DeclaredBy { get; set; } = string.Empty;
    public bool MdApproved { get; set; }
    public string? PostHocPrNumber { get; set; }
    public bool PostHocOverdue { get; set; }
    public bool PostHocRaisedLate { get; set; }
    public string? BoardPackPeriod { get; set; }
}

public class EmergencyFilterParams
{
    /// <summary>AwaitingMd | PostHocOutstanding | BoardPackPending</summary>
    public string? Flag { get; set; }
    public string? Period { get; set; }        // YYYY-MM, on declaration date
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record EmergencyListResult(List<EmergencyRowDto> Items, int Total);

public class EmergencySummaryDto
{
    public int Total { get; set; }
    public int AwaitingMdApproval { get; set; }
    public int PostHocOutstanding { get; set; }
    public int PostHocOverdue { get; set; }
    public int PostHocRaisedLate { get; set; }
    public int BoardPackPending { get; set; }
    public decimal TotalValue { get; set; }
    public decimal ValueThisMonth { get; set; }
}

public record EmergencyActionResult(string Status, string Message, string? EmergencyId = null);
