namespace OperationsService.Core.DTOs.Calibration;

public class CreateReferenceStandardDto
{
    public string  AssetId       { get; set; } = string.Empty;
    public string  Description   { get; set; } = string.Empty;
    public string? NominalValue  { get; set; }
    public string? AccuracyClass { get; set; }
    public string? TraceabilityCertNo { get; set; }
    public string? IssuingBody   { get; set; }
    public double? CertUncertainty { get; set; }
    public int     CoverageFactor  { get; set; } = 2;
    public DateTime? CalibrationDate { get; set; }
    public DateTime? NextDueDate     { get; set; }
}

public class UpdateReferenceStandardDto
{
    public string? Description   { get; set; }
    public string? NominalValue  { get; set; }
    public string? AccuracyClass { get; set; }
    public string? TraceabilityCertNo { get; set; }
    public string? IssuingBody   { get; set; }
    public double? CertUncertainty { get; set; }
    public int?    CoverageFactor  { get; set; }
    public DateTime? CalibrationDate { get; set; }
    public DateTime? NextDueDate     { get; set; }
    public string? Status { get; set; }   // "Active" | "Retired"
}

public class ReferenceStandardReadDto
{
    public string  Id            { get; set; } = string.Empty;
    public string  AssetId       { get; set; } = string.Empty;
    public string  Description   { get; set; } = string.Empty;
    public string? NominalValue  { get; set; }
    public string? AccuracyClass { get; set; }
    public string? TraceabilityCertNo { get; set; }
    public string? IssuingBody   { get; set; }
    public double? CertUncertainty { get; set; }
    public int     CoverageFactor  { get; set; }
    public DateTime? CalibrationDate { get; set; }
    public DateTime? NextDueDate     { get; set; }
    public string  Status        { get; set; } = string.Empty;
    public bool    IsExpired     { get; set; }
    public DateTime CreatedAt    { get; set; }
}

public class CalibrationCertificateReadDto
{
    public string  Id       { get; set; } = string.Empty;
    public string  Number   { get; set; } = string.Empty;
    public string  LabWorkOrderId { get; set; } = string.Empty;
    public string? AssignmentId   { get; set; }
    public string? ServiceRequestId { get; set; }
    public DateTime IssuedAt { get; set; }
    public string   IssuedById { get; set; } = string.Empty;
    public string   SignatoryId   { get; set; } = string.Empty;
    public string   SignatoryName { get; set; } = string.Empty;
    public string? CertJson      { get; set; }
    public string? EnvConditionsJson { get; set; }
    public string? ReferenceStandardIds { get; set; }
    public string? TraceabilityRef { get; set; }
    public DateTime? NextCalibrationDue { get; set; }
}

public class LinkReferenceStandardDto
{
    public string? ReferenceStandardId     { get; set; }   // preferred: a registered standard
    public string? TraceabilityRefFallback { get; set; }   // free-text fallback
}

// ── O6.1 — Issued-certificate register ────────────────────────────────────────
// Backs the certificate register page: expiry tracking for issued certificates and the
// counts an ISO 17025 audit asks for ("how many certificates did you issue, by whom, when").

/// <summary>Where a certificate sits against its recall date, derived at read time.</summary>
public enum CertificateValidity
{
    Valid,      // more than the "expiring" window away from its next-due date
    Expiring,   // inside the warning window (default 60 days)
    Expired,    // past its next-due date
    Withdrawn,  // withdrawn by the lab; excluded from in-force counts
    Unknown,    // no next-due date recorded
}

public class CertificateRegisterItemDto
{
    public string  Id     { get; set; } = string.Empty;
    public string  Number { get; set; } = string.Empty;

    public string? ClientName { get; set; }
    public string? SheetType  { get; set; }
    public string? Equipment  { get; set; }   // read from the CertJson snapshot
    public string? SerialNo   { get; set; }

    public DateTime  IssuedAt           { get; set; }
    public DateTime? NextCalibrationDue { get; set; }
    public string    SignatoryName      { get; set; } = string.Empty;

    public string? LabWorkOrderId   { get; set; }
    public string? AssignmentId     { get; set; }
    public string? ServiceRequestId { get; set; }
    public string? TraceabilityRef  { get; set; }
    /// <summary>CRM customer anchor; when absent, a recall can only fall back to inexact matching.</summary>
    public string? CrmCustomerId    { get; set; }

    public CertificateValidity Validity { get; set; }
    /// <summary>Days until recall; negative once overdue. Null when no due date is recorded.</summary>
    public int? DaysToDue { get; set; }

    // Recall reminder trail — evidence for an audit that the client was chased.
    public DateTime? Recall60SentAt { get; set; }
    public DateTime? Recall30SentAt { get; set; }
    public DateTime? Recall7SentAt  { get; set; }
    public DateTime? WithdrawnAt    { get; set; }
    public string?   WithdrawnReason { get; set; }
}

/// <summary>Audit counts over the same filter as the register list.</summary>
public class CertificateRegisterSummaryDto
{
    public int Total     { get; set; }
    public int Valid     { get; set; }
    public int Expiring  { get; set; }
    public int Expired   { get; set; }
    public int Withdrawn { get; set; }
    public int Unknown   { get; set; }

    /// <summary>Issued per calendar month, "yyyy-MM" → count, oldest first.</summary>
    public Dictionary<string, int> IssuedByMonth     { get; set; } = new();
    public Dictionary<string, int> IssuedBySheetType { get; set; } = new();
    public Dictionary<string, int> IssuedBySignatory { get; set; } = new();
}

public class WithdrawCertificateDto
{
    public string Reason { get; set; } = string.Empty;
}
