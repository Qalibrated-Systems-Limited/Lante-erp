namespace CrmService.Core.DTOs.Deals;

public class CreateDealDto
{
    public string OpportunityId { get; set; } = string.Empty;
    public decimal? ContractValue { get; set; }        // defaults to the linked quotation total / opp value
    public DateTime? ContractStart { get; set; }
    public DateTime? ContractEnd { get; set; }
    public string? PaymentSchedule { get; set; }
}

public class UpdateDealDto
{
    public decimal? ContractValue { get; set; }
    public DateTime? ContractStart { get; set; }
    public DateTime? ContractEnd { get; set; }
    public string? PaymentSchedule { get; set; }
}

public class RegisterContractDto
{
    public string Title { get; set; } = string.Empty;
    public string? ContractType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Value { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal RetentionPct { get; set; }
    public string? FileUrl { get; set; }
    public DateTime? SignedAt { get; set; }
}

public class DealProductDto
{
    public string Id { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class ContractDto
{
    public string Id { get; set; } = string.Empty;
    public string ContractNumber { get; set; } = string.Empty;
    public string DealId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ContractType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Value { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal RetentionPct { get; set; }
    public string? FileUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SignedAt { get; set; }
}

public class DealSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string DealNumber { get; set; } = string.Empty;
    public string OpportunityId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal ContractValue { get; set; }
    public string Currency { get; set; } = "KES";
    public string Status { get; set; } = string.Empty;
    public bool InvoiceTriggered { get; set; }
    public bool HasProject { get; set; }
    public DateTime DealDate { get; set; }
    public DateTime? ContractEnd { get; set; }
}

public class DealDetailDto : DealSummaryDto
{
    public string? CustomerId { get; set; }
    public string? QuotationId { get; set; }
    public DateTime? ContractStart { get; set; }
    public string? PaymentSchedule { get; set; }
    public string? WonBy { get; set; }
    public string? FinanceInvoiceId { get; set; }
    public string? ProjectId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<DealProductDto> Products { get; set; } = new();
    public ContractDto? Contract { get; set; }
}

public class DealFilterParams
{
    public string? Status { get; set; }
    public string? CustomerId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record DealListResult(List<DealSummaryDto> Items, int Total);
public record DealActionResult(string Status, string Message);
