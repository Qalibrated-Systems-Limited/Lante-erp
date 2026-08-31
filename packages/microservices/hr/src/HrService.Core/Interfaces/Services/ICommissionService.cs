using HrService.Core.DTOs.Commission;

namespace HrService.Core.Interfaces.Services;

/// <summary>
/// H11 (COM-001 to COM-009, P29–P31) — sales commission: bands, MD-approved plans, quarterly statements and
/// disputes.
/// <para><b>H11-DEC-1: CRM owns the revenue target and attainment; HR owns the overlay.</b> No method here
/// writes a revenue target — the target is read from CRM, and a statement records what it read.</para>
/// </summary>
public interface ICommissionService
{
    Task<CommissionSummaryDto> GetSummaryAsync(int? year, CancellationToken ct = default);

    // ── Bands (P29 step 29.1) ──
    Task<List<CommissionBandDto>> ListBandsAsync(bool includeInactive);
    /// <summary>Installs the policy's five bands (0/2/3.5/5/7%). Idempotent per effective date.</summary>
    Task<CommissionActionResult> SeedBandsAsync(string userId);
    Task<CommissionActionResult> SaveBandAsync(string? id, SaveCommissionBandDto dto, string userId);

    // ── Plans (P29 step 29.2, COM-002) ──
    /// <summary>Plans with their target and attainment read LIVE from CRM — HR holds no copy.</summary>
    Task<List<CommissionPlanDto>> ListPlansAsync(int? year, string? status, CancellationToken ct = default);
    Task<CommissionActionResult> SavePlanAsync(SaveCommissionPlanDto dto, string userId, CancellationToken ct = default);
    Task<CommissionActionResult> SubmitPlanAsync(string id, string userId);
    /// <summary>MD approval — COM-002 makes it mandatory, and the preparer cannot give it.</summary>
    Task<CommissionActionResult> DecidePlanAsync(string id, DecideCommissionPlanDto dto, string userId, string? userName);

    // ── Statements (P30, COM-004) ──
    Task<List<CommissionStatementDto>> ListStatementsAsync(int? year, string? status, string? employeeId);
    /// <summary>
    /// Computes a quarter's statements from CRM's target and attainment. Refuses outright if CRM cannot be
    /// read — commission computed against a silently zero target would pay nothing and look deliberate.
    /// </summary>
    Task<CommissionActionResult> ComputeStatementsAsync(ComputeStatementsDto dto, string? tenantSchema, string userId, CancellationToken ct = default);
    /// <summary>Approve for payment (a second officer, and not while disputed) or cancel.</summary>
    Task<CommissionActionResult> DecideStatementAsync(string id, DecideStatementDto dto, string userId, string? userName);

    /// <summary>COM-004 — issues the quarter's statements on 1 Apr / 1 Jul / 1 Oct / 1 Jan. Catch-up, not
    /// calendar-triggered: it asks whether the quarter that just ended has statements yet.</summary>
    Task<CommissionSweepResultDto> RunCommissionSweepAsync(string tenantSchema, string userId, CancellationToken ct = default);

    // ── Disputes (P31, COM-006) ──
    Task<List<CommissionDisputeDto>> ListDisputesAsync(string? status);
    /// <summary>Raising a dispute freezes the payment and routes to HR.</summary>
    Task<CommissionActionResult> RaiseDisputeAsync(string statementId, RaiseDisputeDto dto, string? tenantSchema, string userId);
    Task<CommissionActionResult> ResolveDisputeAsync(string id, ResolveDisputeDto dto, string userId, string? userName);
}
