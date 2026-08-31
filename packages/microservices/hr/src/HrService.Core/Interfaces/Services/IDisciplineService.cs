using HrService.Core.DTOs.Discipline;

namespace HrService.Core.Interfaces.Services;

/// <summary>
/// H10 (HR-021 to HR-024, P18 + P19 + P20 + P21) — disciplinary cases, the warning tracker, grievances, and
/// separation with its final-dues calculator.
/// <para><b>The sequence is the fairness.</b> A hearing held before the employee was asked to explain
/// themselves is not a hearing; an outcome recorded without one is not a decision. Every stage refuses to run
/// before the one before it, and stamps who did what and when — this trail is the thing that has to stand up
/// if the case ever leaves the building.</para>
/// <para><b>Deadlines are in WORKING days</b>, on the same calendar leave and attendance use, so a show-cause
/// letter issued on a Thursday does not expire over the weekend.</para>
/// </summary>
public interface IDisciplineService
{
    Task<DisciplineSummaryDto> GetSummaryAsync();

    // ── Disciplinary cases (P18) ──
    Task<List<DisciplinaryCaseDto>> ListCasesAsync(string? status, string? employeeId);
    Task<DisciplinaryCaseDto?> GetCaseAsync(string id);
    Task<DisciplineActionResult> OpenCaseAsync(OpenCaseDto dto, string userId);
    /// <summary>Step 18.2 — issues the show-cause letter and starts the five-working-day response window.</summary>
    Task<DisciplineActionResult> IssueShowCauseAsync(string id, IssueShowCauseDto dto, string? tenantSchema, string userId);
    /// <summary>Step 18.3 — records the employee's response and sets the hearing.</summary>
    Task<DisciplineActionResult> RecordResponseAsync(string id, RecordResponseDto dto, string userId);
    /// <summary>Step 18.4 — the hearing outcome. A warning writes a warning record; a termination opens a
    /// separation.</summary>
    Task<DisciplineActionResult> RecordOutcomeAsync(string id, RecordOutcomeDto dto, string? tenantSchema, string userId, string? userName);
    /// <summary>Step 18.5 — lodge or decide an appeal, within the fourteen-day window.</summary>
    Task<DisciplineActionResult> RecordAppealAsync(string id, RecordAppealDto dto, string userId, string? userName);
    Task<DisciplineActionResult> CloseCaseAsync(string id, string? reason, string userId);

    // ── Warnings (P19) ──
    Task<List<WarningRecordDto>> ListWarningsAsync(string? employeeId, bool includeExpired);
    Task<DisciplineActionResult> IssueWarningAsync(IssueWarningDto dto, string? tenantSchema, string userId);
    /// <summary>The employee's own acknowledgement — not something HR can record on their behalf.</summary>
    Task<DisciplineActionResult> AcknowledgeWarningAsync(string id, AcknowledgeWarningDto dto, string userId);

    // ── Grievances (P20) ──
    Task<List<GrievanceCaseDto>> ListGrievancesAsync(string? status, string? employeeId);
    Task<DisciplineActionResult> SubmitGrievanceAsync(SubmitGrievanceDto dto, string userId);
    Task<DisciplineActionResult> AcknowledgeGrievanceAsync(string id, string userId, string? userName);
    Task<DisciplineActionResult> AssignGrievanceAsync(string id, AssignGrievanceDto dto, string userId);
    Task<DisciplineActionResult> ResolveGrievanceAsync(string id, ResolveGrievanceDto dto, string? tenantSchema, string userId, string? userName);

    // ── Separation and final dues (P21) ──
    Task<List<SeparationDto>> ListSeparationsAsync(string? status, string? employeeId);
    Task<SeparationDto?> GetSeparationAsync(string id);
    /// <summary>Opens a separation and computes the final dues from the leave balance, the salary in force,
    /// notice, and any outstanding advances finance can tell us about.</summary>
    Task<DisciplineActionResult> InitiateSeparationAsync(InitiateSeparationDto dto, string userId, CancellationToken ct = default);
    /// <summary>Adjusts the discretionary lines and recomputes the net.</summary>
    Task<DisciplineActionResult> AdjustDuesAsync(string id, AdjustDuesDto dto, string userId);
    Task<DisciplineActionResult> SubmitSeparationAsync(string id, string userId);
    /// <summary>MD approval, or cancellation. The proposer cannot approve their own.</summary>
    Task<DisciplineActionResult> DecideSeparationAsync(string id, DecideSeparationDto dto, string userId, string? userName);
    /// <summary>Records payment — and only now is the employee deactivated (P21 step 21.5).</summary>
    Task<DisciplineActionResult> PaySeparationAsync(string id, PaySeparationDto dto, string? tenantSchema, string userId, string? userName);

    /// <summary>
    /// The H10 daily sweep, idempotent: warnings past their expiry, three-in-twelve-months escalations
    /// (HR-022), show-cause windows that have lapsed, and grievances past their acknowledgement SLA.
    /// </summary>
    Task<DisciplineSweepResultDto> RunDisciplineSweepAsync(string? tenantSchema, string userId);
}
