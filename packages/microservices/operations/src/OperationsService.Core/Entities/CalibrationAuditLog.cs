namespace OperationsService.Core.Entities;

/// <summary>
/// O6 — ISO-17025 audit trail for key calibration events (standard linked, certificate issued/signed,
/// dispatched). Append-only; one row per significant action with who/when/what.
/// </summary>
public class CalibrationAuditLog : BaseEntity
{
    public string  LabWorkOrderId  { get; set; } = string.Empty;
    public string  Action          { get; set; } = string.Empty;   // e.g. "StandardLinked", "CertificateIssued"
    public string? Detail          { get; set; }
    public string  PerformedById   { get; set; } = string.Empty;
    public string? PerformedByName { get; set; }
}
