using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P13 (CRM-061) — SUBCONTRACTOR_AGREEMENT. Agreements with subcontractors (SUPPLIER seam,
/// stored by name), optionally tied to an ops PROJECT; tracks insurance expiry (alert) and renewal 60/30.</summary>
public class SubcontractorAgreement : BaseEntity
{
    public string AgreementNumber { get; set; } = string.Empty;  // SUB-{yr}-{seq}
    public string SubcontractorName { get; set; } = string.Empty; // SUPPLIER seam (string ref)
    public string? SupplierId { get; set; }                       // procurement SUPPLIER, when known
    public string? ProjectId { get; set; }                        // ops PROJECT (string ref)
    public string? ScopeOfWork { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal? Value { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public SubcontractStatus Status { get; set; } = SubcontractStatus.Active;
    public string? FileUrl { get; set; }
    public DateTime? InsuranceAlertSentAt { get; set; }
    public DateTime? RenewalAlert60SentAt { get; set; }
    public DateTime? RenewalAlert30SentAt { get; set; }
}
