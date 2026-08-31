namespace HrService.Core.DTOs.Leave;

// ── Leave types (P4) ──
public class LeaveTypeDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DaysAllowed { get; set; }
    public decimal? FullPayDays { get; set; }
    public bool IsPaid { get; set; }
    public bool CarriesForward { get; set; }
    public decimal MaxCarryForwardDays { get; set; }
    public int? RequiresDocumentAfterDays { get; set; }
    public string? DocumentTypeRequired { get; set; }
    public bool RequiresHrApproval { get; set; }
    public bool RequiresBoardApproval { get; set; }
    public int BoardApprovalAfterDays { get; set; }
    public bool CountsWorkingDaysOnly { get; set; }
    public bool ProRateFirstYear { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Human-readable chain for this type, e.g. "Line Manager → HR → Board (over 14 days)".</summary>
    public string ApprovalChain { get; set; } = string.Empty;
}

public class SaveLeaveTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DaysAllowed { get; set; }
    public decimal? FullPayDays { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool CarriesForward { get; set; }
    public decimal MaxCarryForwardDays { get; set; }
    public int? RequiresDocumentAfterDays { get; set; }
    /// <summary>Vault document type name, e.g. MedicalCertificate.</summary>
    public string? DocumentTypeRequired { get; set; }
    public bool RequiresHrApproval { get; set; }
    public bool RequiresBoardApproval { get; set; }
    public int BoardApprovalAfterDays { get; set; }
    public bool CountsWorkingDaysOnly { get; set; } = true;
    public bool ProRateFirstYear { get; set; }
    public bool? IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

// ── Entitlements (P4 step 4.2) ──
public class LeaveEntitlementDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string LeaveTypeId { get; set; } = string.Empty;
    public string? LeaveTypeCode { get; set; }
    public string? LeaveTypeName { get; set; }
    public int Year { get; set; }
    public decimal DaysEntitled { get; set; }
    public decimal DaysTaken { get; set; }
    public decimal CarriedForwardDays { get; set; }
    public decimal ForfeitedDays { get; set; }
    public bool WasProRated { get; set; }
    public string? Notes { get; set; }

    /// <summary>Derived: entitled − taken. Not stored, so it cannot drift from the approved requests.</summary>
    public decimal DaysBalance { get; set; }
    /// <summary>Days locked up by requests still in an approval chain.</summary>
    public decimal DaysPending { get; set; }
    /// <summary>What a new request can actually draw on: balance − pending.</summary>
    public decimal DaysAvailable { get; set; }
}

public class AdjustEntitlementDto
{
    /// <summary>Signed adjustment in days — negative to claw back.</summary>
    public decimal Days { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// ── Requests (P5) ──
public class LeaveRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string RequestNumber { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string LeaveTypeId { get; set; } = string.Empty;
    public string? LeaveTypeCode { get; set; }
    public string? LeaveTypeName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public decimal DaysRequested { get; set; }
    public string? Reason { get; set; }
    public string? HandoverNotes { get; set; }
    public string? CoverEmployeeId { get; set; }
    public string? CoverEmployeeName { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? CancellationReason { get; set; }
    public bool DocumentRequired { get; set; }
    public string? RequiredDocumentType { get; set; }
    /// <summary>True once a document of the required type is attached to this request.</summary>
    public bool DocumentAttached { get; set; }
    /// <summary>Which tier the request is sitting with, e.g. "HR".</summary>
    public string? AwaitingRole { get; set; }
    public bool IsCurrentlyOnLeave { get; set; }
    public List<LeaveApprovalStepDto> ApprovalChain { get; set; } = [];
}

public class LeaveApprovalStepDto
{
    public string Id { get; set; } = string.Empty;
    public int Step { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? Comments { get; set; }
    public DateTime? ActionedAt { get; set; }
}

public class CreateLeaveRequestDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string LeaveTypeId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string? Reason { get; set; }
    public string? HandoverNotes { get; set; }
    public string? CoverEmployeeId { get; set; }
}

public class DecideLeaveDto
{
    /// <summary>Approve | Reject</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Comments { get; set; }
}

public class CancelLeaveDto
{
    public string? Reason { get; set; }
}

public class AttachLeaveDocumentDto
{
    public string FileUrl { get; set; } = string.Empty;
    public string? DocumentName { get; set; }
    /// <summary>Defaults to the type the request requires.</summary>
    public string? DocumentType { get; set; }
}

/// <summary>A dry-run of the day count and balance check, so the UI can show the cost before submitting.</summary>
public class LeavePreviewDto
{
    public decimal DaysRequested { get; set; }
    public bool CountsWorkingDaysOnly { get; set; }
    public decimal DaysAvailable { get; set; }
    public bool SufficientBalance { get; set; }
    public bool DocumentRequired { get; set; }
    public string? RequiredDocumentType { get; set; }
    public List<string> ApprovalChain { get; set; } = [];
    public string? Warning { get; set; }
}

// ── Carry-forward (P4/P6) ──
public class LeaveCarryForwardDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? LeaveTypeCode { get; set; }
    public int FromYear { get; set; }
    public int ToYear { get; set; }
    public decimal DaysCarried { get; set; }
    public decimal DaysForfeited { get; set; }
    public decimal DaysExpired { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiredAt { get; set; }
    public string? Notes { get; set; }
    public int DaysToExpiry { get; set; }
}

// ── Summary / results ──
public class LeaveSummaryDto
{
    public int LeaveTypesConfigured { get; set; }
    public int EmployeesWithEntitlements { get; set; }
    public int PendingRequests { get; set; }
    public int AwaitingLineManager { get; set; }
    public int AwaitingHr { get; set; }
    public int AwaitingBoard { get; set; }
    public int MissingRequiredDocument { get; set; }
    public int OnLeaveToday { get; set; }
    public int ApprovedUpcoming { get; set; }
    public int RejectedThisYear { get; set; }
    public decimal DaysTakenThisYear { get; set; }
    public int CarryForwardActive { get; set; }
    public decimal DaysCarriedActive { get; set; }
    public int CarryForwardExpiringIn30Days { get; set; }
}

public record LeaveActionResult(string Status, string Message, string? Id = null);

/// <summary>What the leave side of the daily sweep did.</summary>
public class LeaveSweepResultDto
{
    public int EntitlementsAssigned { get; set; }
    public int CarryForwardRecordsWritten { get; set; }
    public decimal DaysCarried { get; set; }
    public decimal DaysForfeited { get; set; }
    public int CarryForwardExpired { get; set; }
    public decimal DaysExpired { get; set; }
    public string Message { get; set; } = string.Empty;
}
