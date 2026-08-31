namespace OperationsService.Core.Entities;

/// <summary>
/// Field-level audit trail for operations writes (#216), separate from <see cref="CalibrationAuditLog"/>
/// — that one is a purpose-built ISO-17025 event trail keyed to a lab work order (StandardLinked,
/// CertificateIssued, ...), not a generic before/after change log, and this does not replace it.
/// </summary>
public class OperationsAuditLog : BaseEntity
{
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? Details { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
