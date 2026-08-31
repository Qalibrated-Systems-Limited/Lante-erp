using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// O9 — PROJECT_HANDOVER (P10): the 8-step project close-out handover. Completing it requires all four
/// mandatory e-signatures (PM, Department Head, Client Representative, QA). Once completed it is
/// permanent and undeletable (compliance: handover certificates are retained, never removed).
/// </summary>
public class ProjectHandover : BaseEntity
{
    public string ProjectId      { get; set; } = string.Empty;
    public string HandoverNumber { get; set; } = string.Empty;   // HO-{year}-{seq}
    public HandoverStatus Status { get; set; } = HandoverStatus.Draft;

    public string? StepsJson { get; set; }   // 8-step checklist [{step, done, note}]
    public string? ClientName    { get; set; }
    public string? ClientRepName { get; set; }
    public string? Notes         { get; set; }

    public DateTime? CompletedAt { get; set; }
    public string?   CompletedBy { get; set; }
    public bool      IsPermanent { get; set; }   // set true on completion — blocks deletion

    public Project Project { get; set; } = null!;
    public ICollection<ProjectHandoverSignature> Signatures { get; set; } = new List<ProjectHandoverSignature>();
}
