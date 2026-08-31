using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// STAT-006: current Tax Compliance Certificate status, expiry date — alert at 60 days;
// auto-remind Finance to renew via KRA iTax.
public class TaxComplianceCert : BaseEntity
{
    public TccStatus Status { get; set; } = TccStatus.Valid;
    public DateTime ExpiryDate { get; set; }
    public string? ItaxRef { get; set; }
    public int AlertDays { get; set; } = 60;
}
