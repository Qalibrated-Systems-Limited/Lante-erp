using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P12 (CRM-058) — NPS_SURVEY. Annual 0–10 "how likely to recommend" survey; the score maps to
/// Detractor(0–6)/Passive(7–8)/Promoter(9–10). Year stamped for year-over-year tracking.</summary>
public class NpsSurvey : BaseEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public int Year { get; set; }
    public NpsStatus Status { get; set; } = NpsStatus.Pending;
    public int? Score { get; set; }                 // 0..10, null until responded
    public NpsCategory Category { get; set; } = NpsCategory.None;
    public string? Feedback { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}
