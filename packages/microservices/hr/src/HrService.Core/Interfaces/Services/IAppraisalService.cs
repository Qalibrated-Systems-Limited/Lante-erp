using HrService.Core.DTOs.Appraisals;

namespace HrService.Core.Interfaces.Services;

/// <summary>
/// H9 (HR-014 to HR-020, P14 + P15 + P16 + P17) — KPI scorecards and targets, the four-step appraisal
/// workflow, 360-degree feedback, and the improvement plans a low score triggers.
/// <para><b>The scorecard belongs to the ROLE, the targets belong to the PERSON.</b> Everyone doing a job is
/// measured on the same things; what differs is the number each is aiming at.</para>
/// <para><b>Individual 360 ratings are never returned alongside their reviewer.</b> Anonymity is what makes
/// the feedback worth collecting, so it is a property of the read model, not a UI convention.</para>
/// </summary>
public interface IAppraisalService
{
    Task<AppraisalSummaryDto> GetSummaryAsync(int? year);

    // ── Scorecards and targets (P14) ──
    Task<List<KpiScorecardDto>> ListScorecardsAsync(int? year, bool includeInactive);
    Task<KpiScorecardDto?> GetScorecardAsync(string id);
    /// <summary>Creates or replaces a role's scorecard. Refuses to save unless the active items' weights
    /// total exactly 100 (P14 design note).</summary>
    Task<AppraisalActionResult> SaveScorecardAsync(string? id, SaveKpiScorecardDto dto, string userId);
    /// <summary>Raises a target row per employee in the role per item. Idempotent — existing targets keep
    /// their figures.</summary>
    Task<AppraisalActionResult> AssignTargetsAsync(string scorecardId, string userId);

    Task<List<KpiTargetDto>> ListTargetsAsync(int? year, string? employeeId);
    Task<AppraisalActionResult> SetTargetAsync(SetTargetDto dto, string userId);

    // ── Cycles and the appraisal workflow (P15) ──
    Task<List<AppraisalCycleDto>> ListCyclesAsync(int? year);
    /// <summary>Opens a window and raises one appraisal per active employee, each snapshotting the scorecard
    /// and targets in force. One cycle per type per year.</summary>
    Task<AppraisalActionResult> OpenCycleAsync(OpenCycleDto dto, string? tenantSchema, string userId);
    Task<AppraisalActionResult> CloseCycleAsync(string id, string userId);

    Task<List<AppraisalDto>> ListAppraisalsAsync(string? cycleId, string? status, string? employeeId);
    Task<AppraisalDto?> GetAppraisalAsync(string id);

    /// <summary>Step 1 — the employee scores themselves.</summary>
    Task<AppraisalActionResult> SubmitSelfAsync(string id, SubmitAppraisalStepDto dto, string userId);
    /// <summary>Step 2 — the line manager reviews and may adjust. Their score is the verdict.</summary>
    Task<AppraisalActionResult> ReviewAsync(string id, SubmitAppraisalStepDto dto, string userId, string? userName);
    /// <summary>Step 3 — MD sign-off, which is where a below-threshold score raises a PIP (P17 step 17.1).</summary>
    Task<AppraisalActionResult> SignOffAsync(string id, SubmitAppraisalStepDto dto, string? tenantSchema, string userId, string? userName);
    /// <summary>Step 4 — HR records it and the appraisal is closed.</summary>
    Task<AppraisalActionResult> RecordAsync(string id, SubmitAppraisalStepDto dto, string userId, string? userName);

    // ── 360 feedback (P16) ──
    Task<Feedback360SummaryDto?> GetFeedbackAsync(string appraisalId);
    Task<AppraisalActionResult> RequestFeedbackAsync(string appraisalId, Feedback360RequestDto dto, string? tenantSchema, string userId);
    Task<AppraisalActionResult> SubmitFeedbackAsync(string appraisalId, SubmitFeedback360Dto dto, string userId);

    // ── Improvement plans (P17) ──
    Task<List<PipDto>> ListPipsAsync(string? status, string? employeeId);
    Task<AppraisalActionResult> UpdatePipAsync(string id, SavePipDto dto, string userId);
    /// <summary>Improved, extended, or escalated to the disciplinary process (H10).</summary>
    Task<AppraisalActionResult> ClosePipAsync(string id, ClosePipDto dto, string? tenantSchema, string userId, string? userName);
}
