using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P12 (CRM-055) — CLIENT_SATISFACTION_SURVEY. 1–5 rating captured after service delivery;
/// auto-triggered on Module 5 project close (via <see cref="SurveySource.ProjectClose"/>) or raised
/// manually. Linked to the CUSTOMER and, when project-driven, the originating PROJECT (string ref).</summary>
public class ClientSatisfactionSurvey : BaseEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? ProjectId { get; set; }          // Module 5 project (string ref, no hard FK)
    public string? ProjectName { get; set; }
    public SurveySource Source { get; set; } = SurveySource.Manual;
    public SurveyStatus Status { get; set; } = SurveyStatus.Pending;
    public int? Score { get; set; }                 // 1..5, null until responded
    public string? Feedback { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}
