using System.Text.Json;
using OperationsService.Core.DTOs.LabWorkOrders;

namespace OperationsService.Core.Services;

/// <summary>
/// Test-result mathematics for NAWI (OIML R 76-1) and Mass Standards (OIML R 111-1) data sheets:
/// errors, deviations, pass/fail decisions, and advisory range/count flags.
/// Uncertainty budgets live in CertificateCalculationService.
/// </summary>
public class DataSheetCalculationService
{
    private static readonly JsonSerializerOptions Json    = new() { PropertyNameCaseInsensitive = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };
    private static readonly JsonSerializerOptions JsonOut = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ── Public entry points ───────────────────────────────────────────────────

    public string CalculateMass(string rawJson)
    {
        var dto = JsonSerializer.Deserialize<MassRawDataDto>(rawJson, Json);
        if (dto is null) return JsonSerializer.Serialize(new MassCalculatedResultsDto(), JsonOut);

        var result = new MassCalculatedResultsDto();

        // Compute ρ_air once for all blocks so buoyancy corrections are consistent
        double rhoAir = CertificateCalculationService.ComputeAirDensity(dto.EnvironmentalConditions);

        foreach (var block in dto.MeasurementBlocks)
            result.BlockResults.Add(CalculateMassBlock(block, rhoAir));

        result.Uncertainty = CertificateCalculationService.ComputeMassUncertaintyBudget(dto, result.BlockResults);

        return JsonSerializer.Serialize(result, JsonOut);
    }

    public string CalculateNawi(string rawJson)
    {
        var dto = JsonSerializer.Deserialize<NawiRawDataDto>(rawJson, Json)
                  ?? throw new ArgumentException("Invalid NAWI raw data");

        double? e             = ParseScaleInterval(dto.InstrumentDetails?.Division);
        double? d             = ParseScaleInterval(dto.InstrumentDetails?.ReadabilityDivision);
        string  accuracyClass = NormaliseClass(dto.InstrumentDetails?.AccuracyClass);
        double? maxCap        = ParseScaleInterval(dto.InstrumentDetails?.MaximumCapacity);
        double? minCap        = ParseScaleInterval(dto.InstrumentDetails?.MinimumCapacity);
        var     ranges        = dto.InstrumentDetails?.Ranges ?? new();

        // Determine instrument sub-type from eccentricity test pattern
        bool   isWeighbridge = dto.EccentricityTestType is "EndToEnd" or "EndMiddleEnd";
        int    minRepCount   = isWeighbridge ? 3 : 5;
        string testType      = dto.EccentricityTestType ?? "Radial";

        var result = new NawiCalculatedResultsDto();

        if (dto.Eccentricity is not null)
            result.Eccentricity = CalculateEccentricity(dto.Eccentricity, e, accuracyClass, testType, maxCap);

        if (dto.Repeatability is not null)
            result.Repeatability = CalculateRepeatability(dto.Repeatability, e, accuracyClass, minRepCount);

        if (dto.Discrimination is not null)
            result.Discrimination = CalculateDiscrimination(dto.Discrimination, d ?? e);

        if (dto.LinearityRows.Count > 0)
        {
            result.Linearity = CalculateLinearity(dto.LinearityRows, e, accuracyClass, minCap, maxCap, ranges);

            int    repCount  = dto.Repeatability?.Indications.Count(i => i.HasValue) ?? 0;
            double eccMaxDev = result.Eccentricity?.MaximumDeviation ?? 0;
            double eccLoad   = result.Eccentricity is not null && result.Eccentricity.TestLoad > 0
                               ? result.Eccentricity.TestLoad : 1;

            double? certUncG = dto.TestWeights?.CertificateUncertaintyG;
            int     certK    = dto.TestWeights?.CertificateCoverageFactor ?? 2;

            string twClass = NormaliseClass(dto.TestWeights?.Class);
            (double classRho, double classUHalf) = GetClassDensity(twClass);
            double refRho   = dto.TestWeights?.DensityKgM3            ?? classRho;
            double refUHalf = dto.TestWeights?.DensityUncertaintyKgM3 ?? classUHalf;

            CertificateCalculationService.EnrichLinearityWithUncertainty(
                result.Linearity, e,
                result.Repeatability?.StandardDeviation,
                eccMaxDev, eccLoad,
                dto.TestWeights?.Class,
                repCount,
                d, certUncG, certK,
                refRho, refUHalf);
        }

        result.Uncertainty = CertificateCalculationService.ComputeNawiUncertaintyBudget(dto, result, e);
        result.Tolerance   = BuildToleranceSummary(result, e, accuracyClass, minCap);

        return JsonSerializer.Serialize(result, JsonOut);
    }

    // ── NAWI: Eccentricity ────────────────────────────────────────────────────
    // Three patterns (selected by testType):
    //
    //   Radial (Balance / EccentricLoading):
    //     Positions: Centre=Ind1 (reference), N=Ind2, E=Ind3, S=Ind4, W=Ind5
    //     Error(n) = Ind(n) − Ind1   for n=2..5
    //     Errors list: [null, e2, e3, e4, e5]
    //
    //   EndToEnd (Weighbridge):
    //     Positions: FirstEnd=Ind1 (reference), SecondEnd=Ind2
    //     Error = Ind2 − Ind1
    //     Errors list: [null, e2]
    //
    //   EndMiddleEnd (Weighbridge):
    //     Positions: Front=Ind1, Middle=Ind2 (reference), Back=Ind3
    //     Error(front) = Ind1 − Ind2,  Error(back) = Ind3 − Ind2
    //     Errors list: [e_front, null, e_back]   (null marks the reference position)
    //
    // Tolerance (OIML R 76-1 §3.6.2): MaxDeviation ≤ MPE at eccentricity test load
    private static NawiEccentricityResultsDto CalculateEccentricity(
        NawiEccentricityRawDto raw, double? e, string accuracyClass,
        string testType = "Radial", double? maxCapacity = null)
    {
        var    errors = new List<double?>();
        double? maxDev;

        switch (testType)
        {
            case "EndToEnd":
            {
                double? err = raw.Ind1.HasValue && raw.Ind2.HasValue
                    ? Math.Round(raw.Ind2.Value - raw.Ind1.Value, 6)
                    : null;
                errors.Add(null);   // Ind1 = reference (first end)
                errors.Add(err);
                maxDev = err.HasValue ? Math.Round(Math.Abs(err.Value), 6) : null;
                break;
            }
            case "EndMiddleEnd":
            {
                double? ref2   = raw.Ind2;
                double? eFront = raw.Ind1.HasValue && ref2.HasValue
                    ? Math.Round(raw.Ind1.Value - ref2.Value, 6) : null;
                double? eBack  = raw.Ind3.HasValue && ref2.HasValue
                    ? Math.Round(raw.Ind3.Value - ref2.Value, 6) : null;
                errors.Add(eFront);  // index 0 = front deviation
                errors.Add(null);    // index 1 = middle (reference)
                errors.Add(eBack);   // index 2 = back deviation
                var absVals = new[] { eFront, eBack }
                    .Where(x => x.HasValue).Select(x => Math.Abs(x!.Value)).ToList();
                maxDev = absVals.Count > 0 ? Math.Round(absVals.Max(), 6) : null;
                break;
            }
            default: // Radial
            {
                double? ref1 = raw.Ind1;
                var inds = new[] { raw.Ind1, raw.Ind2, raw.Ind3, raw.Ind4, raw.Ind5 };
                for (int i = 0; i < inds.Length; i++)
                {
                    errors.Add(i == 0 ? null :
                        inds[i].HasValue && ref1.HasValue
                            ? Math.Round(inds[i]!.Value - ref1!.Value, 6)
                            : null);
                }
                var nonRefAbs = errors.Skip(1).Where(x => x.HasValue).Select(x => Math.Abs(x!.Value)).ToList();
                maxDev = nonRefAbs.Count > 0 ? Math.Round(nonRefAbs.Max(), 6) : null;
                break;
            }
        }

        double? mpe  = e.HasValue ? Math.Round(NawiMpe(raw.TestLoad, e.Value, accuracyClass), 6) : null;
        bool?   pass = maxDev.HasValue && mpe.HasValue ? maxDev.Value <= mpe.Value : null;

        // OIML R 76-1 §3.6.2.2: eccentricity test load shall be ≥ Max/3
        double? recLoad    = maxCapacity.HasValue ? Math.Round(maxCapacity.Value / 3.0, 3) : null;
        bool?   loadAdequate = recLoad.HasValue ? raw.TestLoad >= recLoad.Value : null;

        // Store raw indications so the certificate table can show actual readings alongside deviations
        List<double?> indications = testType switch
        {
            "EndToEnd"      => new() { raw.Ind1, raw.Ind2 },
            "EndMiddleEnd"  => new() { raw.Ind1, raw.Ind2, raw.Ind3 },
            _               => new() { raw.Ind1, raw.Ind2, raw.Ind3, raw.Ind4, raw.Ind5 },
        };

        return new NawiEccentricityResultsDto
        {
            TestLoad         = raw.TestLoad,
            Indications      = indications,
            Errors           = errors,
            MaximumDeviation = maxDev,
            Mpe              = mpe,
            Pass             = pass,
            RecommendedLoad  = recLoad,
            LoadAdequate     = loadAdequate,
        };
    }

    // ── NAWI: Repeatability ───────────────────────────────────────────────────
    // Error(n) = Indication(n) − TestLoad
    // SD = sample standard deviation of all indications
    // Tolerance (OIML R 76-1 §3.6.1): max |error| ≤ MPE at test load
    // Advisory: InsufficientReadings when n < minRequired (5 balance, 3 weighbridge)
    private static NawiRepeatabilityResultsDto CalculateRepeatability(
        NawiRepeatabilityRawDto raw, double? e, string accuracyClass, int minRequired = 5)
    {
        var errors = raw.Indications
            .Select(ind => ind.HasValue ? (double?)Math.Round(ind.Value - raw.TestLoad, 6) : null)
            .ToList();

        var validInds = raw.Indications.Where(i => i.HasValue).Select(i => i!.Value).ToList();
        double? stdDev = validInds.Count >= 2 ? Math.Round(SampleStdDev(validInds), 6) : null;

        double? mpe = e.HasValue ? Math.Round(NawiMpe(raw.TestLoad, e.Value, accuracyClass), 6) : null;

        var absErrors = errors.Where(x => x.HasValue).Select(x => Math.Abs(x!.Value)).ToList();
        double? maxAbsErr = absErrors.Count > 0 ? absErrors.Max() : null;
        bool?   pass      = maxAbsErr.HasValue && mpe.HasValue ? maxAbsErr.Value <= mpe.Value : null;

        return new NawiRepeatabilityResultsDto
        {
            TestLoad                 = raw.TestLoad,
            Errors                   = errors,
            StandardDeviation        = stdDev,
            Mpe                      = mpe,
            Pass                     = pass,
            InsufficientReadings     = validInds.Count < minRequired,
            MinimumRequiredReadings  = minRequired,
        };
    }

    // ── NAWI: Discrimination ──────────────────────────────────────────────────
    // OIML R 76-1 §4.4: with load on pan, adding 1.4 × d must change the indication.
    // Pass criterion: |I2 − I1| ≥ d  (at least one scale division response).
    // Uses actual display readability d; falls back to e when d is not separately specified.
    private static NawiDiscriminationResultDto CalculateDiscrimination(
        NawiDiscriminationRawDto raw, double? d)
    {
        double? change = raw.Indication1.HasValue && raw.Indication2.HasValue
            ? Math.Round(raw.Indication2.Value - raw.Indication1.Value, 6)
            : null;

        bool? pass = change.HasValue && d.HasValue && d.Value > 0
            ? Math.Abs(change.Value) >= d.Value
            : null;

        return new NawiDiscriminationResultDto
        {
            TestLoad          = raw.TestLoad,
            Indication1       = raw.Indication1,
            Indication2       = raw.Indication2,
            IndicationChange  = change,
            MinRequiredChange = d.HasValue && d.Value > 0 ? d : null,
            Pass              = pass,
        };
    }

    // ── NAWI: Linearity (Accuracy) ────────────────────────────────────────────
    // AsFoundError    = AsFoundIndication − TestLoad
    // DefinitiveError = DefinitiveIndication − TestLoad
    // Tolerance: |DefinitiveError| ≤ MPE at that test load
    // Advisory: OutOfRange when TestLoad < minCapacity or > maxCapacity
    // Per-row uncertainty (U, k, ν_eff) is filled by CertificateCalculationService.
    private static List<NawiLinearityResultRowDto> CalculateLinearity(
        List<NawiLinearityRowDto> rows, double? e, string accuracyClass,
        double? minCapacity = null, double? maxCapacity = null,
        List<NawiRangeDto>? ranges = null)
    {
        return rows.Select(r =>
        {
            // Per-row e: use range-selected value when multi-range is configured
            double? eRow = GetEForLoad(r.TestLoad, ranges, e);

            double? afErr  = r.AsFoundIndication.HasValue
                             ? Math.Round(r.AsFoundIndication.Value - r.TestLoad, 6) : null;
            double? defErr = r.DefinitiveIndication.HasValue
                             ? Math.Round(r.DefinitiveIndication.Value - r.TestLoad, 6) : null;
            double? mpe    = eRow.HasValue ? Math.Round(NawiMpe(r.TestLoad, eRow.Value, accuracyClass), 6) : null;

            bool? outOfRange = (minCapacity.HasValue || maxCapacity.HasValue)
                ? (minCapacity.HasValue && r.TestLoad < minCapacity.Value)
                  || (maxCapacity.HasValue && r.TestLoad > maxCapacity.Value)
                : null;

            return new NawiLinearityResultRowDto
            {
                TestLoad             = r.TestLoad,
                AsFoundIndication    = r.AsFoundIndication,
                AsFoundError         = afErr,
                DefinitiveIndication = r.DefinitiveIndication,
                DefinitiveError      = defErr,
                Mpe                  = mpe,
                AsFoundPass          = afErr.HasValue  && mpe.HasValue ? Math.Abs(afErr.Value)  <= mpe.Value : null,
                DefinitivePass       = defErr.HasValue && mpe.HasValue ? Math.Abs(defErr.Value) <= mpe.Value : null,
                OutOfRange           = outOfRange,
                // Store the per-row scale interval so CertificateCalculationService can use it directly
                ScaleInterval        = ranges?.Count > 0 ? eRow : null,
            };
        }).ToList();
    }

    // Returns the scale interval (e) that applies to a given test load.
    // With multi-range instruments, selects the first range whose MaxLoad >= testLoad.
    // Falls back to globalE when ranges is empty or no matching range exists.
    private static double? GetEForLoad(double testLoad, List<NawiRangeDto>? ranges, double? globalE)
    {
        if (ranges is null || ranges.Count == 0) return globalE;
        var range = ranges
            .Where(r => testLoad <= r.MaxLoad)
            .OrderBy(r => r.MaxLoad)
            .FirstOrDefault();
        return range is null ? globalE : (ParseScaleInterval(range.Division) ?? globalE);
    }

    private static NawiToleranceSummaryDto BuildToleranceSummary(
        NawiCalculatedResultsDto calc, double? e, string accuracyClass, double? minCap = null)
    {
        bool? linPass  = calc.Linearity.Count > 0
            ? calc.Linearity.All(r => r.DefinitivePass == true)
            : null;
        bool? discPass = calc.Discrimination?.Pass;

        // Min capacity advisory (OIML R 76-1 Table 1): does not block OverallPass
        double? minCapReq  = e.HasValue ? Math.Round(MinCapacityRequiredE(accuracyClass) * e.Value, 6) : null;
        bool?   minCapAdequate = minCapReq.HasValue && minCap.HasValue
            ? minCap.Value >= minCapReq.Value : null;

        bool? overall = null;
        if (calc.Eccentricity?.Pass is not null ||
            calc.Repeatability?.Pass is not null ||
            linPass is not null ||
            discPass is not null)
        {
            overall = (calc.Eccentricity?.Pass ?? true)
                   && (calc.Repeatability?.Pass ?? true)
                   && (linPass ?? true)
                   && (discPass ?? true);
        }

        return new NawiToleranceSummaryDto
        {
            ScaleInterval       = e,
            AccuracyClass       = accuracyClass,
            EccentricityPass    = calc.Eccentricity?.Pass,
            RepeatabilityPass   = calc.Repeatability?.Pass,
            LinearityPass       = linPass,
            DiscriminationPass  = discPass,
            MinCapacityRequired = minCapReq,
            MinCapacityAdequate = minCapAdequate,
            OverallPass         = overall,
        };
    }

    // OIML R 76-1 Table 1: minimum capacity as multiples of e
    private static double MinCapacityRequiredE(string normClass) =>
        normClass switch
        {
            "I"    => 100.0,
            "II"   => 20.0,
            "III"  => 20.0,
            "IIII" => 10.0,
            _      => 20.0,
        };

    // ── MASS: Block calculation (ABBA substitution method) ────────────────────
    // Weighing sequence: S₁ → X₁ → X₂ → S₂  (two readings each)
    // S̄ = mean(S1Ind1, S1Ind2, S2Ind1, S2Ind2)
    // X̄ = mean(X1Ind1, X1Ind2, X2Ind1, X2Ind2)
    // Δ  = X̄ − S̄  (balance indication difference, in grams)
    // MPE from OIML R 111-1 Table 1 (in mg)
    //
    // Buoyancy correction (OIML R 111-1 Annex B):
    //   C_buoy = m_kg × ρ_air × (1/ρ_item − 1/ρ_ref) × 1e6  [mg]
    //   Applied only when ItemDensityKgM3 is provided.
    private static MassBlockResultDto CalculateMassBlock(MassMeasurementBlockDto block, double rhoAir = 1.2)
    {
        var stdReadings  = new[] { block.Standard1Ind1, block.Standard1Ind2, block.Standard2Ind1, block.Standard2Ind2 };
        var massReadings = new[] { block.Mass1Ind1,     block.Mass1Ind2,     block.Mass2Ind1,     block.Mass2Ind2     };

        var validStd  = stdReadings .Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var validMass = massReadings.Where(v => v.HasValue).Select(v => v!.Value).ToList();

        double? meanStd  = validStd .Count > 0 ? Math.Round(validStd .Average(), 8) : null;
        double? meanMass = validMass.Count > 0 ? Math.Round(validMass.Average(), 8) : null;
        double? delta    = meanMass.HasValue && meanStd.HasValue
                           ? Math.Round(meanMass.Value - meanStd.Value, 8) : null;

        double? range  = validMass.Count >= 2 ? Math.Round(validMass.Max() - validMass.Min(), 8) : null;
        double? stdDev = validMass.Count >= 2 ? Math.Round(SampleStdDev(validMass), 8)           : null;

        double? mpe = MassR111MpeMilligrams(block.NominalValue, block.Class);

        double? deltaMg = delta.HasValue ? delta.Value * 1000.0 : null;

        // Apply reference standard correction if provided
        double? correctedMg = deltaMg.HasValue && block.ReferenceStdCorrectionMg.HasValue
            ? Math.Round(deltaMg.Value + block.ReferenceStdCorrectionMg.Value, 8)
            : null;

        double? effectiveMg = correctedMg ?? deltaMg;

        // Buoyancy correction value: C_buoy = m × ρ_air × (1/ρ_item − 1/ρ_ref) × 1e6  [mg]
        double? buoyancyMg = null;
        if (block.ItemDensityKgM3.HasValue && block.ItemDensityKgM3.Value > 0 && effectiveMg.HasValue)
        {
            double? nomG = ParseNominalGrams(block.NominalValue);
            if (nomG.HasValue)
            {
                double mKg    = nomG.Value / 1000.0;
                double rhoRef = block.ReferenceStdDensityKgM3 ?? 7950.0;
                buoyancyMg = Math.Round(mKg * rhoAir * (1.0 / block.ItemDensityKgM3.Value - 1.0 / rhoRef) * 1e6, 8);
            }
        }

        double? finalMg = buoyancyMg.HasValue && effectiveMg.HasValue
            ? Math.Round(effectiveMg.Value + buoyancyMg.Value, 8)
            : null;

        double? bestMg  = finalMg ?? effectiveMg;
        bool?   withinTol = bestMg.HasValue && mpe.HasValue
                            ? Math.Abs(bestMg.Value) <= mpe.Value : null;

        return new MassBlockResultDto
        {
            NominalValue          = block.NominalValue,
            Class                 = block.Class,
            SerialNo              = block.SerialNo,
            MeanStandardReading   = meanStd,
            MeanMassReading       = meanMass,
            MassDifference        = delta,
            MassReadingRange      = range,
            MassReadingStdDev     = stdDev,
            MpeMilligrams         = mpe,
            CorrectedDifferenceMg = correctedMg,
            BuoyancyCorrectionMg  = buoyancyMg,
            FinalCorrectionMg     = finalMg,
            WithinTolerance       = withinTol,
        };
    }

    // ── OIML R 76-1 Table 6: NAWI MPE on initial verification ────────────────
    public static double NawiMpe(double testLoad, double e, string accuracyClass)
    {
        if (e <= 0) return 0;
        double loadInE = testLoad / e;
        return MpeFactor(loadInE, NormaliseClass(accuracyClass)) * e;
    }

    internal static double MpeFactor(double loadInE, string normClass) =>
        normClass switch
        {
            "I"    when loadInE <=  50_000 => 0.5,
            "I"    when loadInE <= 200_000 => 1.0,
            "I"                            => 1.5,
            "II"   when loadInE <=   5_000 => 0.5,
            "II"   when loadInE <=  20_000 => 1.0,
            "II"                           => 1.5,
            "III"  when loadInE <=     500 => 0.5,
            "III"  when loadInE <=   2_000 => 1.0,
            "III"                          => 1.5,
            "IIII" when loadInE <=      50 => 0.5,
            "IIII" when loadInE <=     200 => 1.0,
            "IIII"                         => 1.5,
            _                              => 0.5,
        };

    // ── OIML R 111-1 Table 1: Mass MPE on verification (in mg) ───────────────
    public static double? MassR111MpeMilligrams(string nominalValue, string weightClass)
    {
        double? grams = ParseNominalGrams(nominalValue);
        if (grams is null) return null;
        return MassR111Lookup(grams.Value, NormaliseClass(weightClass));
    }

    // Encodes OIML R 111-1 Table 1. Values in mg. Key = nominal grams.
    internal static double? MassR111Lookup(double grams, string cls)
    {
        var table = new (double g, double? e1, double? e2, double? f1, double? f2, double? m1, double? m2, double? m3)[]
        {
            (5000e3,   null,   null, 25000,  80000, 250000,  500000, 2500000),
            (2000e3,   null,   null, 10000,  30000, 100000,  300000, 1000000),
            (1000e3,   null,   1600,  5000,  16000,  50000,  160000,  500000),
            ( 500e3,   null,    800,  2500,   8000,  25000,   80000,  250000),
            ( 200e3,   null,    300,  1000,   3000,  10000,   30000,  100000),
            ( 100e3,   null,    160,   500,   1600,   5000,   16000,   50000),
            (  50e3,   25.0,   80.0,   250,    800,   2500,    8000,   25000),
            (  20e3,   10.0,   30.0,   100,    300,   1000,    3000,   10000),
            (  10e3,    5.0,   16.0,    50,    160,    500,    1600,    5000),
            (   5e3,    2.5,    8.0,    25,     80,    250,     800,    2500),
            (   2e3,    1.0,    3.0,    10,     30,    100,     300,    1000),
            (   1e3,    0.5,    1.6,   5.0,     16,     50,     160,     500),
            (  500,    0.25,   0.8,   2.5,    8.0,     25,      80,     250),
            (  200,    0.10,   0.3,   1.0,    3.0,     10,      30,     100),
            (  100,    0.05,  0.16,   0.5,    1.6,    5.0,      16,      50),
            (   50,    0.03,  0.10,   0.3,    1.0,    3.0,      10,      30),
            (   20,   0.025,  0.08,  0.25,    0.8,    2.5,     8.0,      25),
            (   10,   0.020,  0.06,  0.20,    0.6,    2.0,     6.0,      20),
            (    5,   0.016,  0.05,  0.16,    0.5,    1.6,     5.0,      16),
            (    2,   0.012,  0.04,  0.12,    0.4,    1.2,     4.0,      12),
            (    1,   0.010,  0.03,  0.10,    0.3,    1.0,     3.0,      10),
            (  0.5,   0.008, 0.025,  0.08,   0.25,    0.8,     2.5,    null),
            (  0.2,   0.006, 0.020,  0.06,   0.20,    0.6,     2.0,    null),
            (  0.1,   0.005, 0.016,  0.05,   0.16,    0.5,     1.6,    null),
            ( 0.05,   0.004, 0.012,  0.04,   0.12,    0.4,    null,    null),
            ( 0.02,   0.003, 0.010,  0.03,   0.10,    0.3,    null,    null),
            ( 0.01,   0.003, 0.008, 0.025,   0.08,   0.25,    null,    null),
            (0.005,   0.003, 0.006, 0.020,   0.06,   0.20,    null,    null),
            (0.002,   0.003, 0.006, 0.020,   0.06,   0.20,    null,    null),
            (0.001,   0.003, 0.006, 0.020,   0.06,   0.20,    null,    null),
        };

        var row = table.FirstOrDefault(r => Math.Abs(r.g - grams) / Math.Max(r.g, 1e-9) < 0.01);
        if (row == default) return null;

        return cls switch
        {
            "E1" => row.e1, "E2" => row.e2,
            "F1" => row.f1, "F2" => row.f2,
            "M1" => row.m1, "M2" => row.m2, "M3" => row.m3,
            _    => null,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static double? ParseScaleInterval(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();

        if (s.EndsWith("mg", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(s[..^2].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double mg))
            return mg / 1000.0;

        if (s.EndsWith("kg", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(s[..^2].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double kg))
            return kg * 1000.0;

        if (s.EndsWith('g') &&
            double.TryParse(s[..^1].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double g))
            return g;

        if (double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double plain))
            return plain;

        return null;
    }

    public static double? ParseNominalGrams(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();

        if (s.EndsWith("mg", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(s[..^2].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double mg))
            return mg / 1000.0;

        if (s.EndsWith("kg", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(s[..^2].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double kg))
            return kg * 1000.0;

        if (s.EndsWith('g') &&
            double.TryParse(s[..^1].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double g))
            return g;

        if (double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double plain))
            return plain;

        return null;
    }

    // Returns (nominal density, half-width of rectangular distribution) in kg/m³
    // per OIML R 111-1 Table B.7 / EURAMET cg-18 §7.1.2-7
    public static (double Rho, double UHalf) GetClassDensity(string normalizedClass)
        => normalizedClass switch
        {
            "E1" or "E2" or "F1" => (7950.0, 140.0),
            "M1"                 => (8400.0, 170.0),
            "M2" or "M3"         => (7100.0, 600.0),
            _                    => (7950.0, 140.0),  // safe default (E2/F1 values)
        };

    public static string NormaliseClass(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Trim()
             .Replace("Class", "", StringComparison.OrdinalIgnoreCase)
             .Replace("class", "", StringComparison.OrdinalIgnoreCase)
             .Trim()
             .ToUpperInvariant();
        return s switch
        {
            "4" or "IV" or "IIII" => "IIII",
            "3" or "3L"           => "III",
            "2"                   => "II",
            "1"                   => "I",
            "E1" or "E2" or "F1" or "F2" or "M1" or "M2" or "M3" => s,
            _ => s,
        };
    }

    public static double SampleStdDev(List<double> values)
    {
        if (values.Count < 2) return 0;
        double mean  = values.Average();
        double sumSq = values.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumSq / (values.Count - 1));
    }
}
