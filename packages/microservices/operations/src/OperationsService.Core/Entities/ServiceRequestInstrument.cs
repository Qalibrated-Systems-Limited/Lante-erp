namespace OperationsService.Core.Entities;

/// <summary>
/// O5 — one instrument row on an SRF / CRF form. Each ServiceRequest has 1..N instruments (forms
/// start with 3 rows, allow unlimited additions). Migrated from ticketing.
/// </summary>
public class ServiceRequestInstrument : BaseEntity
{
    public string ServiceRequestId   { get; set; } = string.Empty;
    public int    RowNumber          { get; set; } = 1;

    // ── Common fields (all form types) ───────────────────────────────────────
    public string? Description       { get; set; }
    public string? Manufacturer      { get; set; }
    public string? Model             { get; set; }
    public string? SerialNumber      { get; set; }
    public string? TagNumber         { get; set; }
    public string? Range             { get; set; }
    public string? RangeUnit         { get; set; }
    public string? Condition         { get; set; }
    public string? Remarks           { get; set; }

    // ── Existing certificate info ─────────────────────────────────────────────
    public DateTime? LastCalibrationDate { get; set; }
    public string?   CertificateNumber   { get; set; }

    // ── CRF-NAWI specific fields ──────────────────────────────────────────────
    public string? NawiInstrumentType  { get; set; }
    public string? NawiCapacity        { get; set; }
    public string? NawiScaleInterval   { get; set; }
    public string? NawiAccuracyClass   { get; set; }

    // ── CRF-MASS specific fields ──────────────────────────────────────────────
    public string? MassNominalValue    { get; set; }
    public string? MassAccuracyClass   { get; set; }

    // ── SRF specific fields ───────────────────────────────────────────────────
    public string? ServiceType         { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public ServiceRequest ServiceRequest { get; set; } = null!;
}
