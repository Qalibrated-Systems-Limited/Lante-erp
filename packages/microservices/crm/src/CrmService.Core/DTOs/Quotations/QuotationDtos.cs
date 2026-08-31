namespace CrmService.Core.DTOs.Quotations;

public class CreateQuotationDto
{
    public string OpportunityId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime? ValidUntil { get; set; }
    public string? Notes { get; set; }
}

public class QuotationLineInputDto
{
    public string? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    // Documented reason when this line's price differs from the last sale to the same client (CRM-054).
    public string? PriceExceptionReason { get; set; }
}

public class SaveQuotationLinesDto
{
    public string? Title { get; set; }
    public DateTime? ValidUntil { get; set; }
    public decimal? VatRate { get; set; }
    public string? Notes { get; set; }
    public List<QuotationLineInputDto> Lines { get; set; } = new();
}

public class RejectQuotationDto { public string Reason { get; set; } = string.Empty; }
public class QuotationOutcomeDto { public bool Accepted { get; set; } public string? Reason { get; set; } }

public class QuotationLineDto
{
    public string Id { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal LineTotal { get; set; }
}

public class QuotationSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string QuoteNumber { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsCurrent { get; set; }
    public string OpportunityId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuotationDetailDto : QuotationSummaryDto
{
    public string? CustomerId { get; set; }
    public DateTime IssueDate { get; set; }
    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? DeptHeadApprovedBy { get; set; }
    public DateTime? DeptHeadApprovedAt { get; set; }
    public string? MdApprovedBy { get; set; }
    public DateTime? MdApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public bool RequiresMdApproval { get; set; }
    public List<QuotationLineDto> Lines { get; set; } = new();
}

public class QuotationFilterParams
{
    public string? Status { get; set; }
    public string? OpportunityId { get; set; }
    public string? CustomerId { get; set; }
    public bool CurrentOnly { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// A flagged line where the proposed price differs from the last sale to the same client.
public record PriceExceptionInfo(string ProductRef, decimal LastSalePrice, decimal ProposedPrice, decimal VariancePct);

public class PriceListDto
{
    public string Id { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public decimal StandardRate { get; set; }
    public decimal DiscountLimitPercent { get; set; }
    public string Currency { get; set; } = "KES";
    public DateTime EffectiveDate { get; set; }
    public bool IsActive { get; set; }
}
public class SavePriceListDto
{
    public string ServiceType { get; set; } = string.Empty;
    public decimal StandardRate { get; set; }
    public decimal DiscountLimitPercent { get; set; }
    public string? Currency { get; set; }
}

public record QuotationListResult(List<QuotationSummaryDto> Items, int Total);
public record QuotationActionResult(string Status, string Message);
public record LastSaleInfo(decimal? LastSalePrice, string? QuoteNumber, DateTime? SoldAt);
