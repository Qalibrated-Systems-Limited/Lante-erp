using System.Text.Json;
using FluentAssertions;
using OperationsService.Core.DTOs.LabWorkOrders;
using OperationsService.Core.Services;
using Xunit;

namespace CalibrationMath.Tests;

/// <summary>
/// Unit tests for calibration mathematics.
///
/// Test values are derived from:
///   OIML R 76-1:2006 (E) — Non-automatic weighing instruments
///   OIML R 111-1:2004 (E) — Weights (mass standards)
///   Qalibrated Systems field data (KENAS certificates)
/// </summary>
public class CalibrationMathTests
{
    private readonly DataSheetCalculationService _svc = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // =========================================================================
    // 1. HELPERS
    // =========================================================================

    [Theory]
    [InlineData("1g",      1.0)]
    [InlineData("0.5 g",   0.5)]
    [InlineData("500 mg",  0.5)]
    [InlineData("0.001 kg",1.0)]
    [InlineData("10",      10.0)]
    [InlineData("2 kg",    2000.0)]
    [InlineData("0.1g",    0.1)]
    public void ParseScaleInterval_ConvertsToGrams(string input, double expected)
    {
        var result = DataSheetCalculationService.ParseScaleInterval(input);
        result.Should().BeApproximately(expected, 1e-6);
    }

    [Theory]
    [InlineData("1 kg",    1000.0)]
    [InlineData("500 g",   500.0)]
    [InlineData("20 kg",   20000.0)]
    [InlineData("100g",    100.0)]
    [InlineData("500 mg",  0.5)]
    [InlineData("5000 kg", 5_000_000.0)]
    public void ParseNominalGrams_ParsesCorrectly(string input, double expected)
    {
        var result = DataSheetCalculationService.ParseNominalGrams(input);
        result.Should().BeApproximately(expected, 1e-6);
    }

    [Theory]
    [InlineData("Class III",  "III")]
    [InlineData("class ii",   "II")]
    [InlineData("I",          "I")]
    [InlineData("IIII",       "IIII")]
    [InlineData("3",          "III")]
    [InlineData("E2",         "E2")]
    [InlineData("F1",         "F1")]
    [InlineData("M1",         "M1")]
    public void NormaliseClass_NormalisesCorrectly(string input, string expected)
    {
        DataSheetCalculationService.NormaliseClass(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 1, 2, 3, 4, 1.5811)]  // sample stdev of {0,1,2,3,4}
    [InlineData(2, 2, 2, 2, 2, 0.0)]     // all same → stdev = 0
    public void SampleStdDev_ComputesCorrectly(double a, double b, double c, double d, double e, double expected)
    {
        var vals = new List<double> { a, b, c, d, e };
        DataSheetCalculationService.SampleStdDev(vals).Should().BeApproximately(expected, 1e-3);
    }

    // =========================================================================
    // 2. NAWI MPE TABLE (OIML R 76-1 Table 6)
    // =========================================================================

    // Class III, e = 1g:
    //  0–500e  → ±0.5e  → ±0.5g
    //  500–2000e → ±1.0e → ±1.0g
    //  2000–10000e → ±1.5e → ±1.5g
    [Theory]
    [InlineData(100,   1, "III",  0.5)]   // 100e → ±0.5e
    [InlineData(500,   1, "III",  0.5)]   // 500e boundary (≤ 500e) → ±0.5e
    [InlineData(501,   1, "III",  1.0)]   // just over 500e → ±1.0e
    [InlineData(2000,  1, "III",  1.0)]   // 2000e boundary → ±1.0e
    [InlineData(2001,  1, "III",  1.5)]   // just over 2000e → ±1.5e
    [InlineData(10000, 1, "III",  1.5)]
    // Class II, e = 0.5g
    [InlineData(1000,  0.5, "II", 0.25)]  // 2000e ≤ 5000e → ±0.5e = 0.25g
    [InlineData(5001,  0.5, "II", 0.5)]   // just over 5000e → ±1.0e = 0.5g
    // Class I, e = 0.001g
    [InlineData(25,    0.001, "I", 0.0005)] // 25000e ≤ 50000e → ±0.5e
    [InlineData(100,   0.001, "I", 0.001)]  // 100000e → ±1.0e
    // Class IIII, e = 5g
    [InlineData(100,   5, "IIII", 2.5)]   // 20e ≤ 50e → ±0.5e = 2.5g
    [InlineData(1001,  5, "IIII", 7.5)]   // 200e+ → ±1.5e = 7.5g
    public void NawiMpe_MatchesOimlR76Table6(double testLoad, double e, string cls, double expectedMpe)
    {
        var mpe = DataSheetCalculationService.NawiMpe(testLoad, e, cls);
        mpe.Should().BeApproximately(expectedMpe, 1e-9);
    }

    // =========================================================================
    // 3. MASS MPE TABLE (OIML R 111-1 Table 1)
    // =========================================================================

    [Theory]
    [InlineData("1 kg",  "E1", 0.5)]
    [InlineData("1 kg",  "E2", 1.6)]
    [InlineData("1 kg",  "F1", 5.0)]
    [InlineData("1 kg",  "F2", 16.0)]
    [InlineData("1 kg",  "M1", 50.0)]
    [InlineData("1 kg",  "M2", 160.0)]
    [InlineData("1 kg",  "M3", 500.0)]
    [InlineData("20 kg", "F2", 300.0)]
    [InlineData("500 g", "F2", 8.0)]
    [InlineData("100 g", "M1", 5.0)]
    [InlineData("50 kg", "E1", 25.0)]
    [InlineData("1 mg",  "E1", 0.003)]
    [InlineData("5 g",   "M2", 5.0)]
    public void MassR111Mpe_MatchesOimlR111Table1(string nominalStr, string cls, double expectedMg)
    {
        var mpe = DataSheetCalculationService.MassR111MpeMilligrams(nominalStr, cls);
        mpe.Should().NotBeNull();
        mpe!.Value.Should().BeApproximately(expectedMg, 1e-6);
    }

    [Theory]
    [InlineData("750 g",  "F1")]   // 750g is not an OIML nominal value
    [InlineData("300 g",  "F2")]   // 300g is not an OIML nominal value
    [InlineData("3 kg",   "M1")]   // 3 kg is not an OIML nominal value
    [InlineData("invalid","F2")]   // unparseable string
    public void MassR111Mpe_ReturnsNull_ForUnknownNominalValue(string nominal, string cls)
    {
        var result = DataSheetCalculationService.MassR111MpeMilligrams(nominal, cls);
        result.Should().BeNull($"'{nominal}' is not a standard OIML nominal value");
    }

    // =========================================================================
    // 4. NAWI ECCENTRICITY CALCULATION
    // =========================================================================

    [Fact]
    public void CalculateNawi_Eccentricity_ComputesErrorsAndMaxDeviation()
    {
        // Centre (Ind1) = reference; other positions compared to it
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new NawiInstrumentDetailsDto { Division = "1g", AccuracyClass = "III" },
            Eccentricity = new NawiEccentricityRawDto
            {
                TestLoad = 1000,   // 1000g test load
                Ind1 = 0.0,        // centre — reference
                Ind2 = 0.2,
                Ind3 = -0.3,
                Ind4 = 0.1,
                Ind5 = 0.0,
            },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(
            _svc.CalculateNawi(json), JsonOpts)!;

        var ecc = result.Eccentricity!;
        ecc.Errors[0].Should().BeNull();         // centre — no error
        ecc.Errors[1].Should().BeApproximately( 0.2,  1e-6);
        ecc.Errors[2].Should().BeApproximately(-0.3,  1e-6);
        ecc.Errors[3].Should().BeApproximately( 0.1,  1e-6);
        ecc.Errors[4].Should().BeApproximately( 0.0,  1e-6);
        ecc.MaximumDeviation.Should().BeApproximately(0.3, 1e-6);

        // MPE for 1000g at e=1g, Class III: 1000/1 = 1000e → 500<1000≤2000 → ±1.0e = 1.0g
        ecc.Mpe.Should().BeApproximately(1.0, 1e-6);
        ecc.Pass.Should().BeTrue(); // maxDev 0.3 ≤ MPE 1.0
    }

    [Fact]
    public void CalculateNawi_Eccentricity_FailsWhenDeviationExceedsMpe()
    {
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new NawiInstrumentDetailsDto { Division = "1g", AccuracyClass = "III" },
            Eccentricity = new NawiEccentricityRawDto
            {
                TestLoad = 1000,
                Ind1 = 0.0,
                Ind2 = 0.0,
                Ind3 = 1.5,   // deviation 1.5g > MPE 1.0g
                Ind4 = 0.0,
                Ind5 = 0.0,
            },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        result.Eccentricity!.Pass.Should().BeFalse();
    }

    // =========================================================================
    // 5. NAWI REPEATABILITY CALCULATION
    // =========================================================================

    [Fact]
    public void CalculateNawi_Repeatability_MatchesKenasCertExample()
    {
        // From KENAS cert: SD = 0.11g at test load 20 000g (Class III, e=1g assumed)
        var indications = new List<double?> { 20000.1, 20000.2, 20000.0, 19999.9, 20000.1 };
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new NawiInstrumentDetailsDto { Division = "1g", AccuracyClass = "III" },
            Repeatability = new NawiRepeatabilityRawDto
            {
                TestLoad    = 20000,
                Indications = indications,
            },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        var rep = result.Repeatability!;
        rep.Errors.Should().HaveCount(5);
        rep.Errors[0].Should().BeApproximately(0.1,  1e-5);
        rep.Errors[1].Should().BeApproximately(0.2,  1e-5);
        rep.Errors[2].Should().BeApproximately(0.0,  1e-5);
        rep.Errors[3].Should().BeApproximately(-0.1, 1e-5);
        rep.Errors[4].Should().BeApproximately(0.1,  1e-5);

        // SD of {20000.1, 20000.2, 20000.0, 19999.9, 20000.1} ≈ 0.1140g
        rep.StandardDeviation.Should().BeApproximately(0.1140, 0.001);

        // MPE for 20000g at e=1g, Class III: 20000e → 2000<20000≤10000 → ±1.5g
        rep.Mpe.Should().BeApproximately(1.5, 1e-6);
        rep.Pass.Should().BeTrue(); // max error 0.2 ≤ 1.5
    }

    // =========================================================================
    // 6. NAWI LINEARITY CALCULATION
    // =========================================================================

    [Fact]
    public void CalculateNawi_Linearity_ComputesErrorsAndToleranceChecks()
    {
        // Class III scale, e = 1g
        // Test points: 0, 500, 1000, 2000, 5000g
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new NawiInstrumentDetailsDto { Division = "1g", AccuracyClass = "III" },
            LinearityRows = new List<NawiLinearityRowDto>
            {
                new() { TestLoad = 0,    AsFoundIndication = 0.0,    DefinitiveIndication = 0.0    },
                new() { TestLoad = 500,  AsFoundIndication = 500.3,  DefinitiveIndication = 500.3  },
                new() { TestLoad = 1000, AsFoundIndication = 1000.4, DefinitiveIndication = 1000.4 },
                new() { TestLoad = 2000, AsFoundIndication = 2000.8, DefinitiveIndication = 2000.8 },
                new() { TestLoad = 5000, AsFoundIndication = 5001.2, DefinitiveIndication = 5001.0 },
            },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        var lin = result.Linearity;
        lin.Should().HaveCount(5);

        // Row 0: load=0, error=0, MPE=0.5e=0.5g → pass
        lin[0].AsFoundError.Should().BeApproximately(0.0, 1e-6);
        lin[0].Mpe.Should().BeApproximately(0.5, 1e-6);
        lin[0].AsFoundPass.Should().BeTrue();

        // Row 1: load=500, error=0.3, MPE=0.5g → pass
        lin[1].AsFoundError.Should().BeApproximately(0.3, 1e-5);
        lin[1].Mpe.Should().BeApproximately(0.5, 1e-6);
        lin[1].AsFoundPass.Should().BeTrue();

        // Row 2: load=1000e (1000/1=1000e → ≤2000e) → MPE=1.0g
        lin[2].Mpe.Should().BeApproximately(1.0, 1e-6);
        lin[2].AsFoundError.Should().BeApproximately(0.4, 1e-5);
        lin[2].AsFoundPass.Should().BeTrue();

        // Row 3: load=2000e (boundary ≤2000e) → MPE=1.0g, error=0.8 → pass
        lin[3].Mpe.Should().BeApproximately(1.0, 1e-6);
        lin[3].DefinitiveError.Should().BeApproximately(0.8, 1e-5);
        lin[3].DefinitivePass.Should().BeTrue();

        // Row 4: load=5000e → ≤10000e → MPE=1.5g
        lin[4].Mpe.Should().BeApproximately(1.5, 1e-6);
        lin[4].AsFoundError.Should().BeApproximately(1.2, 1e-5);
        lin[4].AsFoundPass.Should().BeTrue();
        lin[4].DefinitiveError.Should().BeApproximately(1.0, 1e-5);
        lin[4].DefinitivePass.Should().BeTrue();
    }

    [Fact]
    public void CalculateNawi_Linearity_DetectsFailingPoint()
    {
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new NawiInstrumentDetailsDto { Division = "1g", AccuracyClass = "III" },
            LinearityRows = new List<NawiLinearityRowDto>
            {
                new() { TestLoad = 200, AsFoundIndication = 200.6, DefinitiveIndication = 200.6 },
                // error = 0.6g, MPE = 0.5g → FAIL
            },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        result.Linearity[0].AsFoundPass.Should().BeFalse();
        result.Linearity[0].DefinitivePass.Should().BeFalse();
        result.Tolerance!.LinearityPass.Should().BeFalse();
        result.Tolerance.OverallPass.Should().BeFalse();
    }

    // =========================================================================
    // 7. NAWI UNCERTAINTY BUDGET
    // =========================================================================

    [Fact]
    public void CalculateNawi_Uncertainty_ComputesExpectedComponents()
    {
        // Class III, e = 1g, SD = 0.1140g from repeatability
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new NawiInstrumentDetailsDto { Division = "1g", AccuracyClass = "III" },
            TestWeights = new NawiTestWeightsDto { Class = "F2" },
            Repeatability = new NawiRepeatabilityRawDto
            {
                TestLoad    = 5000,
                Indications = new List<double?> { 5000.1, 5000.2, 5000.0, 4999.9, 5000.1 },
            },
            LinearityRows = new List<NawiLinearityRowDto>
            {
                new() { TestLoad = 5000, DefinitiveIndication = 5000.1 },
            },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        var u = result.Uncertainty!;
        // u_resolution = 1 / (2√3) ≈ 0.2887g
        u.UResolution.Should().BeApproximately(1.0 / (2 * Math.Sqrt(3)), 1e-4);
        // u_repeatability should be ≈ 0.1140g (SD of the 5 indications)
        u.URepeatability.Should().NotBeNull().And.BeGreaterThan(0.05);
        // u_combined and u_expanded should be computed
        u.UCombined.Should().BeGreaterThan(0);
        // Tolerance is 1e-4, and the size is not arbitrary. This reconstructs U = k * uc from
        // three values the service rounds INDEPENDENTLY before serialising
        // (CertificateCalculationService.cs:115-117): UCombined to 6dp, UExpanded to 6dp, but
        // CoverageFactor only to 4dp. The error budget is therefore
        //     uc * 5e-5   (k rounded to 4dp)      ~= 2.1e-5   <-- dominant term
        //   + k  * 5e-7   (uc rounded to 6dp)     ~= 1.0e-6
        //   +      5e-7   (UExpanded's own round)
        //   ~= 2.25e-5 at uc ~= 0.42
        // which is why 1e-6 failed and 1e-5 would only pass by luck: k happens to come out an
        // exact 2.0000 for this fixture, so the dominant term vanishes. Any fixture whose vEff
        // yields a non-terminating k (2.0134...) would fail at 1e-5. Nothing is wrong with the
        // uncertainty maths — the assertion has to absorb the DTO's own rounding.
        u.UExpanded.Should().BeApproximately(u.CoverageFactor * u.UCombined!.Value, 1e-4);
        u.CoverageFactor.Should().BeGreaterThanOrEqualTo(2.0);
        u.ConfidenceLevel.Should().Be("95.45%");
    }

    // =========================================================================
    // 8. NAWI TOLERANCE SUMMARY
    // =========================================================================

    [Fact]
    public void CalculateNawi_ToleranceSummary_AllPassWhenWithinLimits()
    {
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new NawiInstrumentDetailsDto { Division = "1g", AccuracyClass = "III" },
            Eccentricity = new NawiEccentricityRawDto
            {
                TestLoad = 1000, Ind1 = 0.0, Ind2 = 0.1, Ind3 = -0.1, Ind4 = 0.2, Ind5 = 0.0,
            },
            Repeatability = new NawiRepeatabilityRawDto
            {
                TestLoad = 5000, Indications = new List<double?> { 5000.1, 5000.0, 5000.2, 5000.1, 5000.0 },
            },
            LinearityRows = new List<NawiLinearityRowDto>
            {
                new() { TestLoad = 1000, AsFoundIndication = 1000.3, DefinitiveIndication = 1000.3 },
                new() { TestLoad = 5000, AsFoundIndication = 5000.5, DefinitiveIndication = 5000.5 },
            },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        var tol = result.Tolerance!;
        tol.ScaleInterval.Should().BeApproximately(1.0, 1e-9);
        tol.AccuracyClass.Should().Be("III");
        tol.EccentricityPass.Should().BeTrue();
        tol.RepeatabilityPass.Should().BeTrue();
        tol.LinearityPass.Should().BeTrue();
        tol.OverallPass.Should().BeTrue();
    }

    // =========================================================================
    // 9. MASS BLOCK CALCULATION (ABBA substitution)
    // =========================================================================

    [Fact]
    public void CalculateMass_Block_ComputesDifferenceAndTolerance()
    {
        // Scenario: 1 kg class F2 test weight on a mass comparator
        // ABBA readings (comparator in mg-level):
        //   S1: 0.000, 0.001  (standard near zero)
        //   X1: 0.014, 0.015  (test weight slightly heavier)
        //   X2: 0.013, 0.016
        //   S2: 0.001, 0.000
        var dto = new MassRawDataDto
        {
            MeasurementBlocks = new List<MassMeasurementBlockDto>
            {
                new()
                {
                    NominalValue    = "1 kg",
                    Class           = "F2",
                    SerialNo        = "TST-001",
                    Standard1Ind1   = 0.000,  Standard1Ind2 = 0.001,
                    Mass1Ind1       = 0.014,  Mass1Ind2     = 0.015,
                    Mass2Ind1       = 0.013,  Mass2Ind2     = 0.016,
                    Standard2Ind1   = 0.001,  Standard2Ind2 = 0.000,
                },
            },
        };
        var rawJson = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result  = JsonSerializer.Deserialize<MassCalculatedResultsDto>(_svc.CalculateMass(rawJson), JsonOpts)!;

        result.BlockResults.Should().HaveCount(1);
        var blk = result.BlockResults[0];

        // S̄ = (0.000+0.001+0.001+0.000)/4 = 0.0005
        blk.MeanStandardReading.Should().BeApproximately(0.0005, 1e-8);
        // X̄ = (0.014+0.015+0.013+0.016)/4 = 0.0145
        blk.MeanMassReading.Should().BeApproximately(0.0145, 1e-8);
        // Δ = 0.0145 - 0.0005 = 0.0140 g = 14.0 mg
        blk.MassDifference.Should().BeApproximately(0.014, 1e-6);

        // Repeatability: range of mass readings = 0.016 - 0.013 = 0.003g
        blk.MassReadingRange.Should().BeApproximately(0.003, 1e-6);

        // MPE for 1 kg F2 = 16 mg (from R111 Table 1)
        blk.MpeMilligrams.Should().BeApproximately(16.0, 1e-6);

        // |Δ| = 14mg ≤ 16mg → within tolerance
        blk.WithinTolerance.Should().BeTrue();
    }

    [Fact]
    public void CalculateMass_Block_DetectsOutOfTolerance()
    {
        // 1 kg E2 weight, MPE = 1.6 mg, but deviation = 2 mg → FAIL
        var dto = new MassRawDataDto
        {
            MeasurementBlocks = new List<MassMeasurementBlockDto>
            {
                new()
                {
                    NominalValue  = "1 kg",
                    Class         = "E2",
                    Standard1Ind1 = 0.000, Standard1Ind2 = 0.000,
                    Mass1Ind1     = 0.002, Mass1Ind2     = 0.002,
                    Mass2Ind1     = 0.002, Mass2Ind2     = 0.002,
                    Standard2Ind1 = 0.000, Standard2Ind2 = 0.000,
                },
            },
        };
        var rawJson = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result  = JsonSerializer.Deserialize<MassCalculatedResultsDto>(_svc.CalculateMass(rawJson), JsonOpts)!;

        var blk = result.BlockResults[0];
        blk.MassDifference.Should().BeApproximately(0.002, 1e-8); // 2 mg
        blk.MpeMilligrams.Should().BeApproximately(1.6, 1e-6);    // 1.6 mg for 1kg E2
        blk.WithinTolerance.Should().BeFalse();
    }

    [Fact]
    public void CalculateMass_MultipleBlocks_EachCalculatedIndependently()
    {
        var dto = new MassRawDataDto
        {
            MeasurementBlocks = new List<MassMeasurementBlockDto>
            {
                new()
                {
                    NominalValue  = "500 g", Class = "F2",
                    Standard1Ind1 = 0.0, Standard1Ind2 = 0.0,
                    Mass1Ind1     = 0.003, Mass1Ind2   = 0.003,
                    Mass2Ind1     = 0.003, Mass2Ind2   = 0.003,
                    Standard2Ind1 = 0.0, Standard2Ind2 = 0.0,
                },
                new()
                {
                    NominalValue  = "1 kg", Class = "F1",
                    Standard1Ind1 = 0.0, Standard1Ind2 = 0.0,
                    Mass1Ind1     = 0.004, Mass1Ind2   = 0.004,
                    Mass2Ind1     = 0.004, Mass2Ind2   = 0.004,
                    Standard2Ind1 = 0.0, Standard2Ind2 = 0.0,
                },
            },
        };
        var rawJson = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result  = JsonSerializer.Deserialize<MassCalculatedResultsDto>(_svc.CalculateMass(rawJson), JsonOpts)!;

        result.BlockResults.Should().HaveCount(2);

        // 500g F2: MPE = 8.0 mg, Δ = 3mg → pass
        result.BlockResults[0].MpeMilligrams.Should().BeApproximately(8.0, 1e-6);
        result.BlockResults[0].WithinTolerance.Should().BeTrue();

        // 1 kg F1: MPE = 5.0 mg, Δ = 4mg → pass
        result.BlockResults[1].MpeMilligrams.Should().BeApproximately(5.0, 1e-6);
        result.BlockResults[1].WithinTolerance.Should().BeTrue();
    }

    // =========================================================================
    // 10. MASS UNCERTAINTY BUDGET
    // =========================================================================

    [Fact]
    public void CalculateMass_Uncertainty_ComputesAllComponents()
    {
        var dto = new MassRawDataDto
        {
            ComparatorDetails = new MassComparatorDetailsDto { Division = "0.001g" },
            MeasurementBlocks = new List<MassMeasurementBlockDto>
            {
                new()
                {
                    NominalValue  = "1 kg", Class = "F2",
                    Standard1Ind1 = 0.000, Standard1Ind2 = 0.000,
                    Mass1Ind1     = 0.014, Mass1Ind2     = 0.015,
                    Mass2Ind1     = 0.013, Mass2Ind2     = 0.016,
                    Standard2Ind1 = 0.000, Standard2Ind2 = 0.000,
                },
            },
        };
        var rawJson = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result  = JsonSerializer.Deserialize<MassCalculatedResultsDto>(_svc.CalculateMass(rawJson), JsonOpts)!;

        var u = result.Uncertainty!;
        // u_resolution = 0.001 / (2√3) ≈ 2.887e-4 g
        u.UResolution.Should().BeApproximately(0.001 / (2 * Math.Sqrt(3)), 1e-7);
        // u_balance should be computed (SD/√n of the 4 mass readings)
        u.UBalance.Should().NotBeNull().And.BeGreaterThan(0);
        // u_buoyancy should be computed for 1 kg weight
        u.UAirBuoyancy.Should().NotBeNull().And.BeGreaterThan(0);
        // u_expanded = k × u_combined (k ≥ 2.0 via Welch-Satterthwaite)
        u.UExpanded.Should().BeApproximately(u.CoverageFactor * u.UCombined!.Value, 1e-8);
        u.CoverageFactor.Should().BeGreaterThanOrEqualTo(2.0);
    }

    [Fact]
    public void CalculateMass_AirBuoyancy_ScalesWithNominalMass()
    {
        // A 10 kg weight should have ~10x the buoyancy uncertainty of a 1 kg weight
        double? uBuoy1kg = GetBuoyancyForNominal("1 kg");
        double? uBuoy10kg = GetBuoyancyForNominal("10 kg");

        uBuoy1kg.Should().NotBeNull();
        uBuoy10kg.Should().NotBeNull();
        (uBuoy10kg!.Value / uBuoy1kg!.Value).Should().BeApproximately(10.0, 0.1);
    }

    private double? GetBuoyancyForNominal(string nominal)
    {
        var dto = new MassRawDataDto
        {
            MeasurementBlocks = new List<MassMeasurementBlockDto>
            {
                new()
                {
                    NominalValue  = nominal, Class = "F2",
                    Standard1Ind1 = 0.0, Standard1Ind2 = 0.0,
                    Mass1Ind1     = 0.01, Mass1Ind2    = 0.01,
                    Mass2Ind1     = 0.01, Mass2Ind2    = 0.01,
                    Standard2Ind1 = 0.0, Standard2Ind2 = 0.0,
                },
            },
        };
        var rawJson = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result  = JsonSerializer.Deserialize<MassCalculatedResultsDto>(_svc.CalculateMass(rawJson), JsonOpts)!;
        return result.Uncertainty?.UAirBuoyancy;
    }

    // =========================================================================
    // 11. WEIGHBRIDGE REAL-DATA (EATL WB2 certificate)
    // =========================================================================
    // Source: Qalibrated Systems cert QSL/QP/18/CERT-NAWI-131
    //   Instrument: AXTX E1105/BMS, 80 000 kg capacity, e = 20 kg, Class III
    //   Reference weights: Class M2 (EAS 1-EAS 20)
    //   End-to-end test load: 10 000 kg (2 positions)
    //   Repeatability test load: 48 180 kg (3 readings)
    //   Linearity: 0–10 000 kg in 1 000 kg steps, all indications on-load

    // All loads in GRAMS so they match the unit from ParseScaleInterval("20 kg") = 20 000 g.
    // Certificate values: U at 0 load ≈ 16 330 g = 16.33 kg, growing slightly with load.
    [Fact]
    public void CalculateNawi_WeighbridgeCertAatl_AllErrorsZero_AllPass()
    {
        // Division "20 kg" → ParseScaleInterval → 20 000 g.  All other values must also be in grams.
        const double eGrams        = 20_000;   // 20 kg
        const double eccLoadG      = 10_000_000; // 10 000 kg
        const double repLoadG      = 48_180_000; // 48 180 kg

        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new NawiInstrumentDetailsDto
            {
                Manufacturer    = "AXTX",
                Model           = "E1105/BMS",
                SerialNo        = "123150142",
                MaximumCapacity = "80000 kg",
                Division        = "20 kg",   // → 20 000 g
                AccuracyClass   = "III",
            },
            TestWeights  = new NawiTestWeightsDto { Class = "M2" },
            Eccentricity = new NawiEccentricityRawDto
            {
                TestLoad = eccLoadG,
                Ind1 = eccLoadG, Ind2 = eccLoadG,   // 2-position, zero deviation
            },
            Repeatability = new NawiRepeatabilityRawDto
            {
                TestLoad    = repLoadG,
                Indications = new List<double?> { repLoadG, repLoadG, repLoadG },
            },
            LinearityRows = new List<NawiLinearityRowDto>
            {
                new() { TestLoad =         0, DefinitiveIndication =         0 },
                new() { TestLoad = 1_000_000, DefinitiveIndication = 1_000_000 },
                new() { TestLoad = 2_000_000, DefinitiveIndication = 2_000_000 },
                new() { TestLoad = 3_000_000, DefinitiveIndication = 3_000_000 },
                new() { TestLoad = 4_000_000, DefinitiveIndication = 4_000_000 },
                new() { TestLoad = 5_000_000, DefinitiveIndication = 5_000_000 },
                new() { TestLoad = 6_000_000, DefinitiveIndication = 6_000_000 },
                new() { TestLoad = 7_000_000, DefinitiveIndication = 7_000_000 },
                new() { TestLoad = 8_000_000, DefinitiveIndication = 8_000_000 },
                new() { TestLoad = 9_000_000, DefinitiveIndication = 9_000_000 },
                new() { TestLoad =10_000_000, DefinitiveIndication =10_000_000 },
            },
        };

        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        // Eccentricity: 2 equal readings → max deviation = 0
        //   10 000 000 g / 20 000 g = 500e → MPE = 0.5e = 10 000 g (= 10 kg)
        result.Eccentricity.Should().NotBeNull();
        result.Eccentricity!.MaximumDeviation.Should().Be(0);
        result.Eccentricity.Mpe.Should().BeApproximately(0.5 * eGrams, 1e-3);
        result.Eccentricity.Pass.Should().BeTrue();

        // Repeatability: 3 identical → SD = 0
        //   48 180 000 g / 20 000 g = 2 409e > 2 000e → MPE = 1.5e = 30 000 g (= 30 kg)
        result.Repeatability.Should().NotBeNull();
        result.Repeatability!.StandardDeviation.Should().Be(0);
        result.Repeatability.Mpe.Should().BeApproximately(1.5 * eGrams, 1e-3);
        result.Repeatability.Pass.Should().BeTrue();

        // Linearity: 11 rows, all errors = 0, all pass
        result.Linearity.Should().HaveCount(11);
        foreach (var row in result.Linearity)
        {
            row.DefinitiveError.Should().Be(0);
            row.DefinitivePass.Should().BeTrue();
        }

        // Per-row U at load 0: U = 2 × √2 × e/(2√3) = 2e/√6 ≈ 16 330 g = 16.33 kg
        double expectedU0 = 2.0 * eGrams / Math.Sqrt(6.0);
        result.Linearity[0].UExpanded.Should().BeApproximately(expectedU0, 1.0);

        // At 1 000 kg (1 000 000 g) M2 reference adds a tiny component → U slightly above U₀
        // OIML R111 covers up to 5 000 kg; above that u_mB = 0 and U stays flat.
        result.Linearity[1].UExpanded.Should().BeGreaterThanOrEqualTo(result.Linearity[0].UExpanded!.Value);
        result.Linearity.Should().AllSatisfy(r => r.UExpanded.Should().BeApproximately(expectedU0, expectedU0 * 0.01));

        // Tolerance summary
        result.Tolerance.Should().NotBeNull();
        result.Tolerance!.ScaleInterval.Should().BeApproximately(eGrams, 1e-3);
        result.Tolerance.AccuracyClass.Should().Be("III");
        result.Tolerance.OverallPass.Should().BeTrue();

        // Global uncertainty resolution component = e/(2√3)
        result.Uncertainty.Should().NotBeNull();
        result.Uncertainty!.UResolution.Should().BeApproximately(eGrams / (2 * Math.Sqrt(3)), 1e-3);
    }

    // =========================================================================
    // 12. EDGE CASES
    // =========================================================================

    // =========================================================================
    // 12. NAWI DISCRIMINATION TEST (OIML R 76-1 §4.4)
    // =========================================================================

    [Fact]
    public void CalculateNawi_Discrimination_PassWhenChangeEqualsD()
    {
        // d = 0.1g (ReadabilityDivision); change = 0.1g → exactly d → PASS (≥)
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new() { Division = "0.5g", ReadabilityDivision = "0.1g", AccuracyClass = "III" },
            Discrimination    = new() { TestLoad = 5000, Indication1 = 5000.0, Indication2 = 5000.1 },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        var disc = result.Discrimination!;
        disc.IndicationChange.Should().BeApproximately(0.1,  1e-9);
        disc.MinRequiredChange.Should().BeApproximately(0.1, 1e-9);  // d (readability)
        disc.Pass.Should().BeTrue();
    }

    [Fact]
    public void CalculateNawi_Discrimination_FailWhenChangeBelowD()
    {
        // d = e = 1g (no separate readability); change = 0.5g < 1g → FAIL
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new() { Division = "1g", AccuracyClass = "III" },
            Discrimination    = new() { TestLoad = 1000, Indication1 = 1000.0, Indication2 = 1000.5 },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        result.Discrimination!.Pass.Should().BeFalse();
        result.Tolerance!.DiscriminationPass.Should().BeFalse();
        result.Tolerance.OverallPass.Should().BeFalse();
    }

    [Fact]
    public void CalculateNawi_Discrimination_FallsBackToEWhenNoReadability()
    {
        // No ReadabilityDivision set → MinRequiredChange = e
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new() { Division = "0.5g", AccuracyClass = "II" },
            Discrimination    = new() { TestLoad = 2000, Indication1 = 2000.0, Indication2 = 2000.6 },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        var disc = result.Discrimination!;
        disc.MinRequiredChange.Should().BeApproximately(0.5, 1e-9);  // falls back to e
        disc.Pass.Should().BeTrue();  // change 0.6 ≥ e 0.5
    }

    // =========================================================================
    // 13. ECCENTRICITY — INDICATIONS STORED IN RESULT
    // =========================================================================

    [Fact]
    public void CalculateNawi_Eccentricity_StoresRawIndicationsInResult()
    {
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new() { Division = "1g", AccuracyClass = "III" },
            Eccentricity      = new()
            {
                TestLoad = 1000,
                Ind1 = 1000.0, Ind2 = 999.8, Ind3 = 1000.0, Ind4 = 1000.4, Ind5 = 999.8,
            },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        var ecc = result.Eccentricity!;
        ecc.Indications.Should().HaveCount(5);
        ecc.Indications[0].Should().BeApproximately(1000.0, 1e-9);  // Centre
        ecc.Indications[1].Should().BeApproximately(999.8,  1e-9);  // Front Left
        ecc.Indications[2].Should().BeApproximately(1000.0, 1e-9);  // Back Left
        ecc.Indications[3].Should().BeApproximately(1000.4, 1e-9);  // Back Right
        ecc.Indications[4].Should().BeApproximately(999.8,  1e-9);  // Front Right
        // Errors and indications must have the same count and same index semantics
        ecc.Indications.Should().HaveSameCount(ecc.Errors);
    }

    // =========================================================================
    // 14. MASS BUOYANCY CORRECTION VALUE (OIML R 111-1 Annex B)
    // =========================================================================

    [Fact]
    public void CalculateMass_BuoyancyCorrection_ComputedWhenItemDensityProvided()
    {
        // 1 kg mass, ρ_item = 7800 kg/m³, ρ_ref = 7950 kg/m³ (default), ρ_air = 1.2 (no P)
        // C_buoy = 1 × 1.2 × (1/7800 − 1/7950) × 1e6
        double rhoItem   = 7800;
        double rhoRef    = 7950;
        double rhoAir    = 1.2;
        double expectedC = 1.0 * rhoAir * (1.0 / rhoItem - 1.0 / rhoRef) * 1e6;  // ≈ 2.90 mg

        var dto = new MassRawDataDto
        {
            MeasurementBlocks = new()
            {
                new()
                {
                    NominalValue    = "1 kg",
                    Class           = "F2",
                    ItemDensityKgM3 = rhoItem,
                    Standard1Ind1   = 0.0,   Standard1Ind2 = 0.0,
                    Mass1Ind1       = 0.010, Mass1Ind2     = 0.010,
                    Mass2Ind1       = 0.010, Mass2Ind2     = 0.010,
                    Standard2Ind1   = 0.0,   Standard2Ind2 = 0.0,
                },
            },
        };
        var rawJson = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result  = JsonSerializer.Deserialize<MassCalculatedResultsDto>(_svc.CalculateMass(rawJson), JsonOpts)!;

        var blk = result.BlockResults[0];
        blk.BuoyancyCorrectionMg.Should().NotBeNull();
        blk.BuoyancyCorrectionMg!.Value.Should().BeApproximately(expectedC, 1e-4);

        // FinalCorrectionMg = effectiveMg + C_buoy = 10 mg + ~2.90 mg
        blk.FinalCorrectionMg.Should().NotBeNull();
        blk.FinalCorrectionMg!.Value.Should().BeApproximately(10.0 + expectedC, 1e-4);
    }

    [Fact]
    public void CalculateMass_BuoyancyCorrection_NullWhenItemDensityAbsent()
    {
        var dto = new MassRawDataDto
        {
            MeasurementBlocks = new()
            {
                new()
                {
                    NominalValue  = "1 kg", Class = "F2",
                    Standard1Ind1 = 0.0, Standard1Ind2 = 0.0,
                    Mass1Ind1     = 0.01, Mass1Ind2    = 0.01,
                    Mass2Ind1     = 0.01, Mass2Ind2    = 0.01,
                    Standard2Ind1 = 0.0, Standard2Ind2 = 0.0,
                },
            },
        };
        var rawJson = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result  = JsonSerializer.Deserialize<MassCalculatedResultsDto>(_svc.CalculateMass(rawJson), JsonOpts)!;

        result.BlockResults[0].BuoyancyCorrectionMg.Should().BeNull();
        result.BlockResults[0].FinalCorrectionMg.Should().BeNull();
    }

    // =========================================================================
    // 15. BIPM AIR DENSITY FORMULA
    // =========================================================================

    [Fact]
    public void ComputeAirDensity_AtStandardConditions_ApproximatesOnePointTwo()
    {
        // At P = 1013.25 hPa, T = 20 °C, H = 50 %: expected ρ_air ≈ 1.204 kg/m³
        var env = new EnvironmentalConditionsDto
        {
            BarometricPressureHPa = 1013.25,
            StartTemperature      = 20.0,
            EndTemperature        = 20.0,
            StartHumidity         = 50.0,
            EndHumidity           = 50.0,
        };
        double rhoAir = CertificateCalculationService.ComputeAirDensity(env);
        rhoAir.Should().BeApproximately(1.2, 0.05);  // within 4 % of standard value
        rhoAir.Should().BeGreaterThan(1.15).And.BeLessThan(1.25);
    }

    [Fact]
    public void ComputeAirDensity_FallsBackTo1Point2_WhenNoPressure()
    {
        CertificateCalculationService.ComputeAirDensity(null).Should().Be(1.2);
        CertificateCalculationService.ComputeAirDensity(new EnvironmentalConditionsDto()).Should().Be(1.2);
    }

    // =========================================================================
    // 16. EDGE CASES
    // =========================================================================

    [Fact]
    public void CalculateNawi_EmptyData_DoesNotThrow()
    {
        var raw  = new NawiRawDataDto();
        var json = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var act  = () => _svc.CalculateNawi(json);
        act.Should().NotThrow();
    }

    [Fact]
    public void CalculateMass_EmptyBlocks_ReturnsEmptyResults()
    {
        var dto     = new MassRawDataDto();
        var rawJson = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result  = JsonSerializer.Deserialize<MassCalculatedResultsDto>(_svc.CalculateMass(rawJson), JsonOpts)!;
        result.BlockResults.Should().BeEmpty();
    }

    [Fact]
    public void NawiMpe_ZeroE_ReturnsZero()
    {
        DataSheetCalculationService.NawiMpe(5000, 0, "III").Should().Be(0);
    }

    [Fact]
    public void CalculateNawi_NullIndications_HandledGracefully()
    {
        var raw = new NawiRawDataDto
        {
            InstrumentDetails = new NawiInstrumentDetailsDto { Division = "1g", AccuracyClass = "III" },
            Repeatability = new NawiRepeatabilityRawDto
            {
                TestLoad    = 1000,
                Indications = new List<double?> { 1000.1, null, 1000.2, null, 1000.0 },
            },
        };
        var json   = JsonSerializer.Serialize(raw, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(_svc.CalculateNawi(json), JsonOpts)!;

        result.Repeatability!.Errors[1].Should().BeNull();
        result.Repeatability.Errors[3].Should().BeNull();
        result.Repeatability.StandardDeviation.Should().NotBeNull();
    }
}
