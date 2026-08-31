using HrService.Core.DTOs.Leave;

namespace HrService.Core.Interfaces.Services;

/// <summary>H3 (HR-005, P4 + P5 + P6) — leave configuration, entitlements, applications with tiered approval,
/// and the year-end carry-forward / 31 March expiry cycle.</summary>
public interface ILeaveService
{
    Task<LeaveSummaryDto> GetSummaryAsync();

    // ── Leave types (P4 step 4.1) ──
    Task<List<LeaveTypeDto>> ListTypesAsync(bool includeInactive);
    Task<LeaveActionResult> CreateTypeAsync(SaveLeaveTypeDto dto, string userId);
    Task<LeaveActionResult> UpdateTypeAsync(string id, SaveLeaveTypeDto dto, string userId);
    /// <summary>Installs the QSL policy defaults (annual 21, sick 7+7, maternity 90, paternity 14,
    /// compassionate 3, study, unpaid) for a tenant that has none. Idempotent per code.</summary>
    Task<LeaveActionResult> SeedDefaultTypesAsync(string userId);

    // ── Entitlements (P4 step 4.2) ──
    Task<List<LeaveEntitlementDto>> ListEntitlementsAsync(string? employeeId, int? year, string? leaveTypeId);
    Task<LeaveActionResult> AssignEntitlementsAsync(int year, string? employeeId, string userId);
    Task<LeaveActionResult> AdjustEntitlementAsync(string entitlementId, AdjustEntitlementDto dto, string userId);

    // ── Requests (P5) ──
    Task<List<LeaveRequestDto>> ListRequestsAsync(string? status, string? employeeId, string? awaitingRole);
    Task<LeaveRequestDto?> GetRequestAsync(string id);
    Task<LeavePreviewDto> PreviewAsync(CreateLeaveRequestDto dto);
    /// <summary><paramref name="tenantSchema"/> is only used to address alert delivery, as in H2.</summary>
    Task<LeaveActionResult> CreateRequestAsync(CreateLeaveRequestDto dto, string? tenantSchema, string userId, string? userName);
    Task<LeaveActionResult> DecideAsync(string requestId, DecideLeaveDto dto, string? tenantSchema, string userId, string? userName);
    Task<LeaveActionResult> CancelAsync(string requestId, CancelLeaveDto dto, string userId);
    Task<LeaveActionResult> AttachDocumentAsync(string requestId, AttachLeaveDocumentDto dto, string userId);

    // ── Carry-forward (P4 step 4.3–4.5 / P6) ──
    Task<List<LeaveCarryForwardDto>> ListCarryForwardAsync(int? toYear, string? status);
    /// <summary>The year-end carry-forward and 31 March expiry, plus current-year entitlement top-up.
    /// Idempotent: a carry-forward row per (employee, type, from-year) is the guard, and expiry only touches
    /// Active rows past their date. Safe to run daily and to trigger manually.</summary>
    Task<LeaveSweepResultDto> RunLeaveSweepAsync(string? tenantSchema, string userId);
}
