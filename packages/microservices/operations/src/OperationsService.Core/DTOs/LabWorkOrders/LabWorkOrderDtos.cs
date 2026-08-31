namespace OperationsService.Core.DTOs.LabWorkOrders;

// ── Lab Work Order summary ────────────────────────────────────────────────────

public class LabWorkOrderDto
{
    public string  Id                    { get; set; } = string.Empty;
    public string  AssignmentId          { get; set; } = string.Empty;
    public string  ServiceRequestId      { get; set; } = string.Empty;
    public string  Status                { get; set; } = string.Empty;
    public string? CalibrationSubType    { get; set; }

    public DateTime? IntakeDate          { get; set; }
    public string?   IntakeTechnicianName{ get; set; }
    public string?   IntakeFormJson      { get; set; }

    public string?   BenchTechnicianName { get; set; }
    public DateTime? BenchStartDate      { get; set; }
    public DateTime? BenchCompletedDate  { get; set; }
    public string?   BenchNotes          { get; set; }
    public string?   BenchChecklistJson  { get; set; }
    public DateTime? BenchSubmittedAt    { get; set; }

    public string?   TmReviewedByName    { get; set; }
    public DateTime? TmReviewedAt        { get; set; }
    public string?   TmApprovalNotes     { get; set; }
    public string?   TmRejectionReason   { get; set; }

    public string?   JobNumber           { get; set; }
    public string?   CertificateNumber   { get; set; }
    public DateTime? CertificateIssuedAt { get; set; }
    public string?   CertificateNotes    { get; set; }

    public DateTime? DispatchDate        { get; set; }
    public string?   DispatchMethod      { get; set; }
    public string?   DispatchNotes       { get; set; }
    public string?   ReceivedBy          { get; set; }

    public LabDataSheetDto? DataSheet    { get; set; }

    public DateTime  CreatedAt           { get; set; }
}

// ── Intake ────────────────────────────────────────────────────────────────────

public class RecordIntakeDto
{
    // Customer (may differ from the SR customer)
    public string  CustomerName          { get; set; } = string.Empty;
    public string? CustomerAddress       { get; set; }
    public string? ContactPersonName     { get; set; }
    public string? ContactPersonPhone    { get; set; }

    // Delivery person
    public string? DeliveryPersonName    { get; set; }
    public string? DeliveryPersonId      { get; set; }

    // Equipment
    public string  ConditionOnReceipt    { get; set; } = string.Empty;
    public string  JobDescription        { get; set; } = string.Empty;
    public string? AccessoriesReceived   { get; set; }

    // Calibration sub-type (only required when assignment type is NAWI)
    public string? CalibrationSubType    { get; set; }  // "BalanceAndPlatform" | "Weighbridge"

    // Instrument location (e.g. "QSL Laboratory")
    public string? Location              { get; set; }
    public string? StickerNumber         { get; set; }

    // Received by (auto-filled from token on backend, but can be overridden)
    public string? ReceivedByName        { get; set; }
    public DateTime? IntakeDate          { get; set; }
}

// ── Bench stage ───────────────────────────────────────────────────────────────

public class RecordBenchDto
{
    public string? TechnicianId   { get; set; }
    public string? TechnicianName { get; set; }
    public string? Notes          { get; set; }
}

public class CompleteBenchDto
{
    public string? Notes         { get; set; }
    public string? ChecklistJson { get; set; }
}

// ── TM review ─────────────────────────────────────────────────────────────────

public class TmReviewLabDto
{
    public bool    Approve            { get; set; }
    public string  CertificateNumber  { get; set; } = string.Empty;
    public string? JobNumber          { get; set; }
    public string? Notes              { get; set; }
    public string? RejectionReason    { get; set; }
    public string? TmName             { get; set; }
}

// ── Dispatch ──────────────────────────────────────────────────────────────────

public class RecordDispatchDto
{
    public DateTime? DispatchDate   { get; set; }
    public string?   DispatchMethod { get; set; }
    public string?   Notes          { get; set; }
    public string?   ReceivedBy     { get; set; }
}

// ── Data sheet ────────────────────────────────────────────────────────────────

public class LabDataSheetDto
{
    public string  Id                    { get; set; } = string.Empty;
    public string  SheetType             { get; set; } = string.Empty;
    public string  Status                { get; set; } = string.Empty;
    public string? RawDataJson           { get; set; }
    public string? CalculatedResultsJson { get; set; }
    public DateTime? SubmittedAt         { get; set; }
    public string?   SubmittedByName     { get; set; }
    public DateTime  CreatedAt           { get; set; }
}

public class SaveDataSheetDto
{
    public string RawDataJson { get; set; } = string.Empty;  // serialised type-specific payload
    public bool   Submit      { get; set; } = false;          // true = move to AwaitingTmReview
}

// ── Shared sub-types ─────────────────────────────────────────────────────────

public class EnvironmentalConditionsDto
{
    public double? StartTemperature      { get; set; }
    public double? StartHumidity         { get; set; }
    public double? EndTemperature        { get; set; }
    public double? EndHumidity           { get; set; }
    public double? BarometricPressureHPa { get; set; }  // P — needed for true ρ_air formula
}

// ── MASS data sheet ───────────────────────────────────────────────────────────

public class MassRawDataDto
{
    public EnvironmentalConditionsDto?   EnvironmentalConditions { get; set; }
    public MassComparatorDetailsDto?     ComparatorDetails       { get; set; }
    public List<MassMeasurementBlockDto> MeasurementBlocks       { get; set; } = new();
    public string? CalibrationDoneBy { get; set; }
    public string? CheckedBy         { get; set; }
}

public class MassComparatorDetailsDto
{
    public string? Model    { get; set; }
    public string? SerialNo { get; set; }
    public string? Capacity { get; set; }
    public string? Division { get; set; }
}

public class MassMeasurementBlockDto
{
    public string  NominalValue         { get; set; } = string.Empty;
    public string  Class                { get; set; } = string.Empty;
    public string? SerialNo             { get; set; }
    public string? ReferenceStdSerialNo { get; set; }
    public string? ReferenceStdClass    { get; set; }
    public double? ReferenceStdCorrectionMg              { get; set; }  // certified correction value (mg) added to Δ
    public double? ReferenceStdDensityKgM3               { get; set; }  // ρ_ref — defaults to 7 950 kg/m³ if unset
    public double? ReferenceStdCertificateUncertaintyG   { get; set; }  // U_cert of reference std (grams); replaces MPE-based u_ref when set
    public int     ReferenceStdCertificateCoverageFactor { get; set; } = 2;  // k_cert stated on the certificate
    public string? ReferenceStdCertificateNo             { get; set; }  // traceability cert number shown on the calibration certificate
    public double? ItemDensityKgM3 { get; set; }  // ρ_item — density of item under calibration; enables buoyancy correction value
    // Row 1: Standard — two repeat readings
    public double? Standard1Ind1 { get; set; }
    public double? Standard1Ind2 { get; set; }
    // Row 2: Mass — two repeat readings
    public double? Mass1Ind1     { get; set; }
    public double? Mass1Ind2     { get; set; }
    // Row 3: Mass — two repeat readings
    public double? Mass2Ind1     { get; set; }
    public double? Mass2Ind2     { get; set; }
    // Row 4: Standard — two repeat readings
    public double? Standard2Ind1 { get; set; }
    public double? Standard2Ind2 { get; set; }
}

// ── NAWI data sheet (Balance/Platform and Weighbridge share same DTO) ─────────

public class NawiRawDataDto
{
    public EnvironmentalConditionsDto?  EnvironmentalConditions { get; set; }
    public NawiInstrumentDetailsDto?    InstrumentDetails       { get; set; }
    public NawiTestWeightsDto?          TestWeights             { get; set; }
    public NawiEccentricityRawDto?      Eccentricity            { get; set; }
    public NawiRepeatabilityRawDto?     Repeatability           { get; set; }
    public NawiDiscriminationRawDto?    Discrimination          { get; set; }
    public List<NawiLinearityRowDto>    LinearityRows           { get; set; } = new();
    public string? EccentricityTestType { get; set; }  // weighbridge: "EndToEnd" | "EndMiddleEnd" | "EccentricLoading"
    public string? LabNo             { get; set; }       // "NAWI (Site)" | "NAWI (Internal)"
    public string? CalibrationDoneBy { get; set; }
    public string? CheckedBy         { get; set; }
}

// ── NAWI discrimination test raw data ────────────────────────────────────────
// OIML R 76-1 §4.4: instrument shall respond to a load change of 1.4 × d.
// Technician places TestLoad, records Indication1, adds small weight, records Indication2.
public class NawiDiscriminationRawDto
{
    public double  TestLoad    { get; set; }
    public double? Indication1 { get; set; }  // reading before adding small weight
    public double? Indication2 { get; set; }  // reading after adding small weight
}

public class NawiInstrumentDetailsDto
{
    public string? EquipmentType       { get; set; }       // e.g. "Platform Electronic Balance"
    public string? RangeType           { get; set; }       // "Single" | "MultiRange" | "MultiInterval"
    public string? Manufacturer        { get; set; }
    public string? Model               { get; set; }
    public string? SerialNo            { get; set; }
    public string? MaximumCapacity     { get; set; }
    public string? Division            { get; set; }       // e — verification scale interval (MPE divisor)
    public string? MinimumCapacity     { get; set; }
    public string? AccuracyClass       { get; set; }
    public string? ReadabilityDivision { get; set; }       // d — actual display readability (if finer than e); null → d = e
    public List<NawiRangeDto> Ranges   { get; set; } = new(); // multi-range: ascending (MaxLoad, Division) pairs
}

public class NawiRangeDto
{
    public double MaxLoad  { get; set; }                   // upper bound of this range (same unit as TestLoad)
    public string Division { get; set; } = string.Empty;  // e for this range, e.g. "2g"
}

public class NawiTestWeightsDto
{
    public string? Class        { get; set; }
    public string? SerialNumber { get; set; }
    public double? CertificateUncertaintyG   { get; set; }      // U_cert from weight certificate (grams) — enables u(cal_ref)
    public int     CertificateCoverageFactor { get; set; } = 2; // k_cert stated on the certificate
    public double? DensityKgM3              { get; set; }       // ρ_ref — defaults to class-based lookup if unset
    public double? DensityUncertaintyKgM3   { get; set; }       // half-width of rectangular ρ_ref distribution; defaults to class-based lookup
    public string? TraceabilityCertificateNo { get; set; }      // reference std traceability cert number shown on the certificate
}

public class NawiEccentricityRawDto
{
    public double  TestLoad { get; set; }
    public double? Ind1     { get; set; }  // Centre — reference point
    public double? Ind2     { get; set; }
    public double? Ind3     { get; set; }
    public double? Ind4     { get; set; }
    public double? Ind5     { get; set; }
}

public class NawiRepeatabilityRawDto
{
    public double        TestLoad    { get; set; }
    public List<double?> Indications { get; set; } = new(); // 5 for balance, 3 for weighbridge
}

public class NawiLinearityRowDto
{
    public double  TestLoad              { get; set; }
    public double? AsFoundIndication     { get; set; }
    public double? DefinitiveIndication  { get; set; }
    public int?    NumberOfReadings      { get; set; }  // No. of measurement readings per row (informational)
}

// ── NAWI calculated results ───────────────────────────────────────────────────

public class NawiCalculatedResultsDto
{
    public NawiEccentricityResultsDto?     Eccentricity    { get; set; }
    public NawiRepeatabilityResultsDto?    Repeatability   { get; set; }
    public NawiDiscriminationResultDto?    Discrimination  { get; set; }
    public List<NawiLinearityResultRowDto> Linearity       { get; set; } = new();
    public NawiUncertaintyDto?             Uncertainty     { get; set; }
    public NawiToleranceSummaryDto?        Tolerance       { get; set; }
}

// ── NAWI discrimination result ────────────────────────────────────────────────
public class NawiDiscriminationResultDto
{
    public double  TestLoad           { get; set; }
    public double? Indication1        { get; set; }
    public double? Indication2        { get; set; }
    public double? IndicationChange   { get; set; }  // I2 − I1
    public double? MinRequiredChange  { get; set; }  // d — instrument must change by ≥ 1 division
    public bool?   Pass               { get; set; }  // |IndicationChange| ≥ d
}

public class NawiEccentricityResultsDto
{
    public double        TestLoad          { get; set; }
    public List<double?> Indications       { get; set; } = new(); // raw readings per position (same index order as Errors)
    public List<double?> Errors            { get; set; } = new(); // index 0 = null (reference), 1-4 = calculated
    public double?       MaximumDeviation  { get; set; }
    public double?       Mpe               { get; set; }  // ±MPE in same units
    public bool?         Pass              { get; set; }
    public double?       RecommendedLoad   { get; set; }  // ≥ Max/3 per OIML R 76-1 §3.6.2.2
    public bool?         LoadAdequate      { get; set; }  // advisory: TestLoad >= RecommendedLoad
}

public class NawiRepeatabilityResultsDto
{
    public double        TestLoad          { get; set; }
    public List<double?> Errors            { get; set; } = new(); // Error = Indication - TestLoad
    public double?       StandardDeviation        { get; set; }
    public double?       Mpe                      { get; set; }  // repeatability limit = MPE at test load
    public bool?         Pass                     { get; set; }  // max|error| ≤ MPE
    public bool?         InsufficientReadings     { get; set; }  // n < minimum required
    public int?          MinimumRequiredReadings  { get; set; }  // 5 for balance, 3 for weighbridge
}

public class NawiLinearityResultRowDto
{
    public double  TestLoad              { get; set; }
    public double? AsFoundIndication     { get; set; }
    public double? AsFoundError          { get; set; }
    public double? DefinitiveIndication  { get; set; }
    public double? DefinitiveError       { get; set; }
    public double? Mpe                   { get; set; }
    public bool?   AsFoundPass           { get; set; }
    public bool?   DefinitivePass        { get; set; }
    public bool?   OutOfRange            { get; set; }  // test load outside Min..Max capacity
    public double? ScaleInterval         { get; set; }  // e for this row — from range selection; null = single-range instrument
    // Per-row uncertainty (matches the "Expanded measurement uncertainty" column in the certificate)
    public double? UCombined                  { get; set; }
    public double? UExpanded                  { get; set; }
    public double  CoverageFactor             { get; set; } = 2.0;
    public double? EffectiveDegreesOfFreedom  { get; set; }
}

// Uncertainty budget for NAWI calibration — EURAMET cg-18 §7.1.3, Welch-Satterthwaite k
public class NawiUncertaintyDto
{
    public double? UResolution              { get; set; }  // e / (2√3)                   — u(dig0) = u(digL)
    public double? URepeatability           { get; set; }  // SD                           — Type A
    public double? UEccentricity            { get; set; }  // scaled eccentricity
    public double? UBuoyancyRefMass         { get; set; }  // MPE_R111 / (4√3)             — §7.1.2-5c
    public double? UDriftRefMass            { get; set; }  // MPE_R111 / (3√3)             — §7.1.2-11
    public double? UConvectionRefMass       { get; set; }  // = UDriftRefMass              — §7.1.2-13
    public double? UDensityRefMass          { get; set; }  // buoyancy from ρ_ref unc       — EURAMET cg-18 §7.1.2-7
    public double? UCalibrationRef          { get; set; }  // U_cert / k_cert              — reference weight certificate (8th component)
    public double? UCombined                { get; set; }  // √(2u_do²+u_rep²+u_ecc²+u_mB²+u_mD²+u_mconv²+u_mc²+u_cal²)
    public double? UExpanded                { get; set; }  // k × UCombined
    public double? EffectiveDegreesOfFreedom { get; set; } // Welch-Satterthwaite ν_eff
    public double  CoverageFactor           { get; set; } = 2.0;
    public string  ConfidenceLevel          { get; set; } = "95.45%";
}

// Overall tolerance check summary
public class NawiToleranceSummaryDto
{
    public double?  ScaleInterval       { get; set; }   // e (parsed from instrument details)
    public string?  AccuracyClass       { get; set; }
    public bool?    EccentricityPass    { get; set; }
    public bool?    RepeatabilityPass   { get; set; }
    public bool?    LinearityPass       { get; set; }   // all definitive errors pass
    public bool?    DiscriminationPass  { get; set; }
    // Advisory: MinCapacity — does not block OverallPass
    public double?  MinCapacityRequired { get; set; }   // OIML R 76-1 minimum (class × e)
    public bool?    MinCapacityAdequate { get; set; }   // stated MinimumCapacity >= required
    public bool?    OverallPass         { get; set; }
}

// ── MASS calculated results ───────────────────────────────────────────────────

public class MassCalculatedResultsDto
{
    public List<MassBlockResultDto> BlockResults { get; set; } = new();
    public MassUncertaintyDto?      Uncertainty  { get; set; }
}

public class MassBlockResultDto
{
    public string  NominalValue          { get; set; } = string.Empty;
    public string  Class                 { get; set; } = string.Empty;
    public string? SerialNo              { get; set; }

    // Substitution weighing averages (ABBA: S1 → X1 → X2 → S2)
    public double? MeanStandardReading   { get; set; }  // S̄ = avg(S1, S2 readings)
    public double? MeanMassReading       { get; set; }  // X̄ = avg(X1, X2 readings)
    public double? MassDifference        { get; set; }  // Δ = X̄ − S̄  (balance units)

    // Repeatability of mass readings
    public double? MassReadingRange      { get; set; }  // max − min of all X readings
    public double? MassReadingStdDev     { get; set; }  // sample SD of all X readings

    // Tolerance per OIML R 111-1 Table 1
    public double? MpeMilligrams         { get; set; }  // ±δm in mg (null if class/value unknown)
    public double? CorrectedDifferenceMg  { get; set; }  // Δ_mg + ref correction; set when ReferenceStdCorrectionMg is provided
    public double? BuoyancyCorrectionMg   { get; set; }  // C_buoy = m × ρ_air × (1/ρ_item − 1/ρ_ref); set when ItemDensityKgM3 is provided
    public double? FinalCorrectionMg      { get; set; }  // effectiveMg + C_buoy (best estimate of mass difference)
    public bool?   WithinTolerance        { get; set; }  // |FinalCorrectionMg ?? effectiveMg| ≤ MPE_mg

    // Per-block expanded uncertainty (certificate column)
    public double? UCombined             { get; set; }
    public double? UExpanded             { get; set; }
}

// Uncertainty budget for mass standard calibration
public class MassUncertaintyDto
{
    public double UAsssumedDensityKgM3     { get; set; } = 7950;
    public double DensityUncertaintyKgM3   { get; set; } = 70;
    public double? UBalance                { get; set; }  // SD/√n           — repeatability (Type A)
    public double? UResolution             { get; set; }  // d / (2√3)       — comparator resolution
    public double? UConvection             { get; set; }  // d / (2√3)       — convection (rectangular ±d/2)
    public double? UReferenceWeight        { get; set; }  // MPE_R111 / (2√3) — reference standard tolerance
    public double? UAirBuoyancy            { get; set; }  // m × ρ_air × u(ρ_t) / ρ_t²
    public double? UCombined                   { get; set; }  // √(uBal²+uRes²+uConv²+uRef²+uBuoy²)
    public double? UExpanded                   { get; set; }  // U = k × u_c
    public double? EffectiveDegreesOfFreedom   { get; set; }  // Welch-Satterthwaite ν_eff
    public double  CoverageFactor              { get; set; } = 2.0;
    public string  ConfidenceLevel             { get; set; } = "95.45%";
}

// ── Certificate output ────────────────────────────────────────────────────────

// Unified certificate DTO assembled from IntakeFormJson + RawDataJson + CalculatedResultsJson.
// Returned by GET certificate-data; used to render the certificate page.
public class CalibrationCertificateDto
{
    // Certificate header
    public string?   CertificateNumber   { get; set; }
    public string?   JobNumber           { get; set; }
    public DateTime? CertificateIssuedAt { get; set; }
    public string?   Notes               { get; set; }
    public string    SheetType           { get; set; } = string.Empty;

    // Customer details (from IntakeFormJson)
    public string?   CustomerName        { get; set; }
    public string?   CustomerAddress     { get; set; }
    public string?   ContactPersonName   { get; set; }
    public string?   ContactPersonPhone  { get; set; }
    public string?   JobDescription      { get; set; }
    public string?   Location            { get; set; }
    public string?   StickerNumber       { get; set; }

    // Lab classification
    public string?   LabNo               { get; set; }

    // Sign-off
    public string?   CalibrationDoneBy   { get; set; }
    public string?   CheckedBy           { get; set; }
    public string?   TmApprovedBy        { get; set; }
    public DateTime? TmApprovedAt        { get; set; }

    // Type-specific data
    public NawiCertificateSectionDto? Nawi { get; set; }
    public MassCertificateSectionDto? Mass { get; set; }
}

public class NawiCertificateSectionDto
{
    // Instrument details
    public string? EquipmentType   { get; set; }
    public string? RangeType       { get; set; }
    public string? Manufacturer    { get; set; }
    public string? Model           { get; set; }
    public string? SerialNo        { get; set; }
    public string? MaximumCapacity { get; set; }
    public string? MinimumCapacity { get; set; }
    public string? Division        { get; set; }
    public string? AccuracyClass   { get; set; }

    // Test weights
    public string? TestWeightClass             { get; set; }
    public string? TestWeightSerialNo          { get; set; }
    public string? TestWeightCertificateNo     { get; set; }  // traceability cert number (KEBS/MET/...)

    // Environmental conditions
    public EnvironmentalConditionsDto? EnvironmentalConditions { get; set; }

    // Calculated results
    public NawiEccentricityResultsDto?     Eccentricity   { get; set; }
    public NawiRepeatabilityResultsDto?    Repeatability  { get; set; }
    public NawiDiscriminationResultDto?    Discrimination { get; set; }
    public List<NawiLinearityResultRowDto> Linearity      { get; set; } = new();
    public NawiUncertaintyDto?             Uncertainty    { get; set; }
    public NawiToleranceSummaryDto?        Tolerance      { get; set; }
}

public class MassCertificateSectionDto
{
    // Comparator details
    public string? ComparatorModel    { get; set; }
    public string? ComparatorSerialNo { get; set; }
    public string? ComparatorDivision { get; set; }

    // Reference standard summary (first block, used in certificate text)
    public string? ReferenceStdClass         { get; set; }
    public string? ReferenceStdSerialNo      { get; set; }
    public string? ReferenceStdCertificateNo { get; set; }  // traceability cert number (KEBS/MET/...)

    // Environmental conditions
    public EnvironmentalConditionsDto? EnvironmentalConditions { get; set; }

    // Calculated results
    public List<MassBlockResultDto> BlockResults { get; set; } = new();
    public MassUncertaintyDto?      Uncertainty  { get; set; }
}
