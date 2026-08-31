using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// O6 — REFERENCE_STANDARD: a calibration standard the lab owns (test weights, etc.) with its own
/// external traceability certificate and a next-due date. A calibration certificate can only be issued
/// against a valid, non-expired standard (traceability gate). The background sweep alerts 60/30 days
/// before a standard's calibration falls due.
/// </summary>
public class ReferenceStandard : BaseEntity
{
    public string  AssetId          { get; set; } = string.Empty;   // internal asset/serial tag
    public string  Description      { get; set; } = string.Empty;
    public string? NominalValue     { get; set; }   // e.g. "1 kg", "F1 20 kg set"
    public string? AccuracyClass    { get; set; }   // OIML class, etc.

    // External traceability (from the accredited lab that calibrated this standard)
    public string? TraceabilityCertNo   { get; set; }
    public string? IssuingBody          { get; set; }   // KEBS / accredited lab
    public double? CertUncertainty      { get; set; }   // U_cert
    public int     CoverageFactor       { get; set; } = 2;

    public DateTime? CalibrationDate { get; set; }
    public DateTime? NextDueDate     { get; set; }   // when this standard's own calibration expires

    public ReferenceStandardStatus Status { get; set; } = ReferenceStandardStatus.Active;

    // O6 — expiry-alert bookkeeping (one alert per threshold, fired by the background sweep).
    public DateTime? Alert60SentAt { get; set; }
    public DateTime? Alert30SentAt { get; set; }
}
