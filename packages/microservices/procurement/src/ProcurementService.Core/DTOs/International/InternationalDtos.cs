namespace ProcurementService.Core.DTOs.International;

// ── Create / update ──
public class CreateIntlPoDto
{
    public string PoId { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal PurchasePriceFx { get; set; }
    /// <summary>Optional override. Left empty, the rate is read from Finance; if Finance cannot be reached the
    /// request is rejected rather than guessed, so supply it explicitly in that case.</summary>
    public decimal? ExchangeRate { get; set; }
    /// <summary>Ordered quantity — the divisor for the per-unit landed cost until Stores confirms receipt.</summary>
    public decimal QuantityOrdered { get; set; }
    public string? ProformaInvoiceUrl { get; set; }
}

public class ShipmentDto
{
    public string? BlNumber { get; set; }
    public DateTime? Eta { get; set; }
    public string? ProformaInvoiceUrl { get; set; }
}

public class RequestTtDto
{
    public decimal AmountFx { get; set; }
}

public class AddComponentDto
{
    /// <summary>Freight | ImportDuty | Clearing | PortCharges | Insurance | Other</summary>
    public string ComponentType { get; set; } = string.Empty;
    public decimal AmountFx { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    /// <summary>Optional override; otherwise read from Finance at <see cref="IncurredOn"/>.</summary>
    public decimal? ExchangeRate { get; set; }
    public DateTime? IncurredOn { get; set; }
    public string? BlNumber { get; set; }
    public DateTime? Eta { get; set; }
    public string? Notes { get; set; }
}

public class DeclareCustomsDto
{
    public string IdfNumber { get; set; } = string.Empty;
    public string? EntryNumber { get; set; }
    public decimal ImportDutyKes { get; set; }
    public decimal ClearingAgentFeeKes { get; set; }
    public decimal PortChargesKes { get; set; }
    public DateTime? DeclaredAt { get; set; }
    public string? Notes { get; set; }
}

// ── Reads ──
public class LandedCostComponentDto
{
    public string Id { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public decimal AmountFx { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public decimal ExchangeRate { get; set; }
    public decimal KesAmount { get; set; }
    public DateTime IncurredOn { get; set; }
    public string? BlNumber { get; set; }
    public DateTime? Eta { get; set; }
    public string? Notes { get; set; }
    public bool FromCustoms { get; set; }
}

public class CustomsDeclarationDto
{
    public string Id { get; set; } = string.Empty;
    public string IdfNumber { get; set; } = string.Empty;
    public string? EntryNumber { get; set; }
    public decimal ImportDutyKes { get; set; }
    public decimal ClearingAgentFeeKes { get; set; }
    public decimal PortChargesKes { get; set; }
    public DateTime DeclaredAt { get; set; }
    public string? Notes { get; set; }
}

public class IntlPoReadDto
{
    public string Id { get; set; } = string.Empty;
    public string PoId { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;
    public decimal PurchasePriceFx { get; set; }
    public decimal ExchangeRateAtOrder { get; set; }
    public decimal PurchasePriceKes { get; set; }
    public string? ProformaInvoiceUrl { get; set; }

    public decimal? TtAmountFx { get; set; }
    public DateTime? TtRequestedAt { get; set; }
    public string? TtApprovedBy { get; set; }
    public DateTime? TtApprovedAt { get; set; }
    public DateTime? TtSentAt { get; set; }
    public string? TtVoucherNo { get; set; }

    public string? BlNumber { get; set; }
    public DateTime? Eta { get; set; }

    public decimal TotalLandedCostKes { get; set; }
    public decimal LandedCostPerUnitKes { get; set; }
    public decimal QuantityBasis { get; set; }
    /// <summary>True once Stores has confirmed receipt, so the per-unit figure divides by the received
    /// quantity rather than the ordered one.</summary>
    public bool QuantityFromReceipt { get; set; }

    public string Status { get; set; } = string.Empty;
    public string PoStatus { get; set; } = string.Empty;
    public string ReceiptStatus { get; set; } = "NotReceived";
    public DateTime CreatedAt { get; set; }

    public List<LandedCostComponentDto> Components { get; set; } = new();
    public CustomsDeclarationDto? Customs { get; set; }

    // Workflow affordances
    public bool CanRequestTt { get; set; }
    public bool CanApproveTt { get; set; }
    public bool CanSendTt { get; set; }
}

public class IntlPoRowDto
{
    public string Id { get; set; } = string.Empty;
    public string PoId { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal PurchasePriceFx { get; set; }
    public decimal PurchasePriceKes { get; set; }
    public decimal TotalLandedCostKes { get; set; }
    public decimal LandedCostPerUnitKes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? BlNumber { get; set; }
    public DateTime? Eta { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class IntlFilterParams
{
    public string? Status { get; set; }
    public string? CurrencyCode { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record IntlListResult(List<IntlPoRowDto> Items, int Total);

public class IntlSummaryDto
{
    public int Total { get; set; }
    public int AwaitingTtApproval { get; set; }
    public int TtApprovedNotSent { get; set; }
    public int InTransit { get; set; }
    public int AwaitingCustoms { get; set; }
    public int Costed { get; set; }
    public decimal TotalLandedValueKes { get; set; }
    public decimal TtOutstandingKes { get; set; }
}

public record IntlActionResult(string Status, string Message, string? IntlPoId = null);

/// <summary>An FX rate offered to the UI so the user sees what will be applied before committing.</summary>
public record CurrencyRateDto(string Code, string? Name, decimal Rate, bool FromFinance);
