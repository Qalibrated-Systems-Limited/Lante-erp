namespace CrmService.Core.Entities;

/// <summary>P4 — PRICE_EXCEPTION_LOG (CRM-054). Permanent audit trail: whenever a quoted unit price
/// differs from the last sale price to the same client for the same item, a documented reason is
/// required and logged here before the quotation can proceed.</summary>
public class PriceExceptionLog : BaseEntity
{
    public string QuotationId { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string ProductRef { get; set; } = string.Empty;   // productId or description
    public decimal LastSalePrice { get; set; }
    public decimal ProposedPrice { get; set; }
    public decimal VariancePct { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string LoggedBy { get; set; } = string.Empty;
}
