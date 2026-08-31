using SubcontractsService.Core.Enums;

namespace SubcontractsService.Core.Entities;

// SUB-007: certified invoices, retention balances, WHT deductions, and payment dates per
// subcontractor per project. Retention rate/cap and release-stage rules are contract-specific and
// not modeled here (#367) — the server records the figures off the payment certificate rather than
// deriving them.
public class PaymentRetention : BaseEntity
{
    public string AwardId { get; set; } = string.Empty;
    public decimal CertifiedAmount { get; set; }
    public decimal RetentionHeld { get; set; }
    public decimal Wht { get; set; }
    public RetentionStatus Status { get; set; } = RetentionStatus.Certified;
    public DateTime? PaidOn { get; set; }
}
