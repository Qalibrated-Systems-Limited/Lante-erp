namespace OperationsService.Core.Entities;

/// <summary>
/// O6 — CALIBRATION_CERTIFICATE: the immutable record of an issued calibration certificate. Snapshots
/// the full certificate payload (CertJson) plus the system number, authorized signatory, environmental
/// conditions and the reference standards used, at the moment of issue — so the issued artifact never
/// drifts even if the underlying LWO/data-sheet data changes later (ISO-17025 audit requirement).
/// </summary>
public class CalibrationCertificate : BaseEntity
{
    public string  Number           { get; set; } = string.Empty;   // system CAL-{year}-{seq}
    public string  LabWorkOrderId   { get; set; } = string.Empty;
    public string? AssignmentId     { get; set; }
    public string? ServiceRequestId { get; set; }

    public DateTime IssuedAt   { get; set; }
    public string   IssuedById { get; set; } = string.Empty;

    // Authorized signatory (Permission:calibration.sign)
    public string  SignatoryId   { get; set; } = string.Empty;
    public string  SignatoryName { get; set; } = string.Empty;

    public string? CertJson            { get; set; }   // frozen snapshot of CalibrationCertificateDto
    public string? EnvConditionsJson   { get; set; }
    public string? ReferenceStandardIds { get; set; }  // CSV of ReferenceStandard ids used
    public string? TraceabilityRef      { get; set; }  // free-text fallback ref when no registered standard

    public DateTime? NextCalibrationDue { get; set; }  // recall date reported to CRM

    // Denormalised from CertJson at issue so the certificate register can search, filter and group
    // in the database. CertJson stays the authoritative snapshot — these are a read-model copy, and
    // are backfilled for pre-existing rows by the AddCertificateRegister migration.
    public string? ClientName { get; set; }
    public string? SheetType  { get; set; }   // "Mass" | "NawiBalance" | "NawiWeighbridge"

    /// <summary>
    /// CRM customer id carried over from the originating service request at issue, so a recall can
    /// resolve the client's current address exactly rather than guessing from their name.
    /// </summary>
    public string? CrmCustomerId { get; set; }

    // Client recall reminders. Each tier fires at most once, mirroring ReferenceStandard's
    // Alert60SentAt/Alert30SentAt so both expiry engines behave the same way.
    public DateTime? Recall60SentAt { get; set; }
    public DateTime? Recall30SentAt { get; set; }
    public DateTime? Recall7SentAt  { get; set; }

    /// <summary>
    /// Set when a certificate is withdrawn (superseded or issued in error). A withdrawn certificate
    /// still appears in the register — an ISO 17025 audit trail must not lose records — but it stops
    /// generating recall reminders and is excluded from "in force" counts.
    /// </summary>
    public DateTime? WithdrawnAt     { get; set; }
    public string?   WithdrawnReason { get; set; }
}
