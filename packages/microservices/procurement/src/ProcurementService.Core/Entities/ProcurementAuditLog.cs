using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>DS AUDIT_LOG (PROC-005) — an immutable trail of every procurement state change. In P1 it
/// captures all ASR events (create, document upload, conflict, approval, blacklist, gift). Later phases
/// append their own actions. Retained per the module retention policy (7yr; blacklist indefinite).</summary>
public class ProcurementAuditLog : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;      // "Supplier" | "Gift" | ...
    public string EntityId { get; set; } = string.Empty;
    public AsrAuditAction Action { get; set; }
    public string? Detail { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public string? PerformedByName { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
