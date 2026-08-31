namespace SubcontractsService.Core.DTOs.Retentions;

// SUB-007: certified invoices, retention balances, WHT deductions.
public class PaymentRetentionReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AwardId { get; set; } = string.Empty;
    public decimal CertifiedAmount { get; set; }
    public decimal RetentionHeld { get; set; }
    public decimal Wht { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidOn { get; set; }
}

public class CreatePaymentRetentionDto
{
    public string AwardId { get; set; } = string.Empty;
    public decimal CertifiedAmount { get; set; }
    public decimal RetentionHeld { get; set; }
    public decimal Wht { get; set; }
}

// Core-field edit — AwardId is fixed after creation; Status/PaidOn stay workflow-controlled
// (see MarkPaid).
public class UpdatePaymentRetentionDto
{
    public decimal CertifiedAmount { get; set; }
    public decimal RetentionHeld { get; set; }
    public decimal Wht { get; set; }
}
