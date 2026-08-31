using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P13 (CRM-060) — FRAMEWORK_AGREEMENT. Umbrella supply/service agreements with periodic
/// performance-review dates; renewal alerts at 60/30 days and a review-due alert.</summary>
public class FrameworkAgreement : BaseEntity
{
    public string AgreementNumber { get; set; } = string.Empty;  // FRM-{yr}-{seq}
    public string CounterpartyName { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? PerformanceReviewDate { get; set; }
    public decimal? Value { get; set; }
    public FrameworkStatus Status { get; set; } = FrameworkStatus.Active;
    public string? FileUrl { get; set; }
    public DateTime? ReviewAlertSentAt { get; set; }
    public DateTime? RenewalAlert60SentAt { get; set; }
    public DateTime? RenewalAlert30SentAt { get; set; }
}
