using HrService.Core.DTOs.Learning;

namespace HrService.Core.Interfaces.Services;

/// <summary>
/// H7 (HR-025 to HR-036, P22 + P23 + P25 + P26) — learning and development: individual plans, the training
/// hours ledger, mandatory-training compliance, the knowledge-sharing register and the L&amp;D budget.
/// <para><b>HR owns the rule, not the record</b> (HR-DEC-5). The mandatory-training matrix lives here; the
/// evidence for HSE and anti-bribery is read from the services that own it. Only Data Protection — which has
/// no other home — is satisfied from HR's own training log.</para>
/// <para>P24 (certificate vault) is deliberately absent: HR-DEC-6 collapsed it into H1's
/// <c>EmployeeCertification</c>, whose 30-day expiry alerts H2 already raises.</para>
/// </summary>
public interface ILearningService
{
    Task<LearningSummaryDto> GetSummaryAsync(int? year, CancellationToken ct = default);

    // ── Learning & development plans (P22) ──
    Task<List<LdpDto>> ListPlansAsync(int? year, string? status, string? employeeId);
    Task<LdpDto?> GetPlanAsync(string id);
    /// <summary>Creates or replaces the employee's plan for the year. A plan already approved is not editable.</summary>
    Task<LearningActionResult> SavePlanAsync(SaveLdpDto dto, string userId);
    Task<LearningActionResult> SubmitPlanAsync(string id, string userId);
    /// <summary>Line-manager decision. The person who submitted a plan cannot approve it.</summary>
    Task<LearningActionResult> DecidePlanAsync(string id, DecideLdpDto dto, string userId, string? userName);

    // ── Training events and hours (P23) ──
    Task<List<TrainingEventDto>> ListTrainingAsync(int? year, string? departmentId, string? employeeId);
    /// <summary>
    /// Logs an event and its attendance in one go: each attendee gets an hours entry, any matching LDP
    /// objective closes, a mandatory requirement is satisfied, and the department's budget takes the cost.
    /// </summary>
    Task<LearningActionResult> LogTrainingAsync(SaveTrainingEventDto dto, string? tenantSchema, string userId);
    Task<List<TrainingHoursSummaryDto>> ListHoursAsync(int? year, string? departmentId);

    // ── Mandatory training (P23, HR-029/034) ──
    Task<List<MandatoryRequirementDto>> ListRequirementsAsync(bool includeInactive);
    /// <summary>Installs HSE, anti-bribery and data protection, each pointed at the service that owns its
    /// evidence. Idempotent per code.</summary>
    Task<LearningActionResult> SeedRequirementsAsync(string userId);
    Task<LearningActionResult> SaveRequirementAsync(string? id, SaveMandatoryRequirementDto dto, string userId);

    /// <summary>Where every employee stands against every requirement, evidence read live from hse and
    /// compliance. A source that cannot be read is reported as unknown, never as a lapse.</summary>
    Task<List<EmployeeComplianceDto>> GetComplianceAsync(string? employeeId, CancellationToken ct = default);

    /// <summary>The H8 gate: may this employee be proposed for a salary increment (HR-029/HR-035)?</summary>
    Task<IncrementEligibilityDto?> GetIncrementEligibilityAsync(string employeeId, CancellationToken ct = default);

    // ── Knowledge sharing (P25) ──
    Task<List<KnowledgeSharingSessionDto>> ListSessionsAsync(int? year, int? month);
    /// <summary>Logs the session and awards its hours to every attendee through the same training ledger, so
    /// internal sharing counts towards the annual target exactly as an external course does.</summary>
    Task<LearningActionResult> LogSessionAsync(SaveKnowledgeSharingDto dto, string userId);

    // ── L&D budgets (P26) ──
    Task<List<LdBudgetDto>> ListBudgetsAsync(int? year);
    Task<LearningActionResult> SaveBudgetAsync(SaveLdBudgetDto dto, string userId);

    /// <summary>
    /// The L&amp;D red flags, run daily and idempotent: the 31 January LDP reminder and 15 February MD
    /// escalation (HR-036), zero hours by 30 June (HR-033), mandatory training breached beyond its grace
    /// period (HR-034), fewer than two knowledge-sharing sessions in a completed month (HR-030), and the 80%
    /// and 100% budget thresholds (HR-031).
    /// </summary>
    Task<LearningSweepResultDto> RunLearningSweepAsync(string? tenantSchema, string userId, CancellationToken ct = default);
}
