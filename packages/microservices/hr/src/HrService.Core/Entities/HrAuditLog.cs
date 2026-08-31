using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>ARCH-007 — immutable trail of HR state changes (the DFD's AUDIT_LOG data store, referenced by
/// nearly every process). Employee records, payroll and disciplinary actions all need to be answerable for
/// after the fact, so each phase appends its own actions rather than logging into another module.</summary>
public class HrAuditLog : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;    // "Employee" | "EmployeeDocument" | "OrgChart" | ...
    public string EntityId { get; set; } = string.Empty;
    public HrAuditAction Action { get; set; }
    public string? Detail { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public string? PerformedByName { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
