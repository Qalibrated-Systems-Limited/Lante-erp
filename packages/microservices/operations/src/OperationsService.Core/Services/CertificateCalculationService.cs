using OperationsService.Core.DTOs.LabWorkOrders;

namespace OperationsService.Core.Services;

/// <summary>
/// Uncertainty budget calculations used to produce the two certificate columns:
///   "Coverage factor k"  and  "Expanded measurement uncertainty U".
///
/// NAWI (non-automatic weighing instruments):
///   EURAMET cg-18 v4.0, §7.1.3-1a — 7-component GUM model
///   GUM Appendix B3 / JCGM 100:2008 — Welch-Satterthwaite effective degrees of freedom
///
/// Mass standards:
///   OIML R 111-1 Annex C.6 — 5-component GUM model
/// </summary>
public sealed class CertificateCalculationService
{
    // ── NAWI: per-row uncertainty (certificate linearity table) ───────────────
    //
    // 7 uncertainty components per EURAMET cg-18 §7.1.3-1a:
    //
    //   u(dig0) = e / (2√3)               resolution at no-load          [rectangular ±e/2]
    //   u(digL) = e / (2√3)               resolution at load L           [rectangular ±e/2]
    //   u(rep)  = s_r                     repeatability std. deviation    [Type A]
    //   u(ecc)  = ΔI_ecc × L/(L_ecc×2√3) eccentricity at load L         [rectangular]
    //   u(mB)   = MPE_R111 / (4√3)        buoyancy of ref. mass §7.1.2-5c [rectangular]
    //   u(mD)   = MPE_R111 / (3√3)        drift of ref. mass  §7.1.2-11  [rectangular, D≈MPE/3]
    //   u(mconv)= u(mD)                   convection          §7.1.2-13
    //
    //   u_c = √(u_do²+u_dL²+u_rep²+u_ecc²+u_mB²+u_mD²+u_mconv²)
    //
    // Welch-Satterthwaite (GUM App. B3):
    //   ν_eff = u_c⁴ × ν_rep / u_rep⁴    (only u_rep has finite dof; ν_rep = n_rep − 1)
    //   When u_rep = 0 (all readings identical): ν_eff = 510  → k ≈ 2.005 ≈ 2.0
    //
    // Coverage factor k from GUM Table G.2 at p = 95.45 %
    public static void EnrichLinearityWithUncertainty(
        List<NawiLinearityResultRowDto> rows,
        double? e,
        double? uRepeatability,
        double eccMaxDev,
        double eccLoad,
        string? testWeightClass,
        int repeatabilityCount,
        double? readability         = null,    // d — actual display resolution; null → d = e
        double? certUncertaintyG    = null,    // U_cert of reference weight (grams) — enables u(cal_ref)
        int certCoverageFactor      = 2,       // k_cert
        double refDensityKgM3       = 7950.0,  // ρ_ref — nominal density of reference mass
        double refDensityHalfWidth  = 140.0)   // half-width of rectangular ρ_ref distribution (kg/m³)
    {
        bool hasAnyE = (e.HasValue && e.Value > 0) || rows.Any(r => r.ScaleInterval > 0);
        if (!hasAnyE) return;

        double sqrt3   = Math.Sqrt(3);
        double uRep    = uRepeatability ?? 0;
        string twClass = DataSheetCalculationService.NormaliseClass(testWeightClass);
        int    n       = repeatabilityCount;
        double uCalRef = certUncertaintyG.HasValue && certUncertaintyG.Value > 0
            ? certUncertaintyG.Value / certCoverageFactor
            : 0;

        foreach (var row in rows)
        {
            // Per-row scale interval: row-level wins (multi-range), fall back to global e
            double eRow = (row.ScaleInterval.HasValue && row.ScaleInterval.Value > 0)
                ? row.ScaleInterval.Value
                : (e ?? 0);
            if (eRow <= 0) continue;

            // u(dig0) and u(digL) use actual display readability d when provided, else e
            double dRow = readability.HasValue && readability.Value > 0 ? readability.Value : eRow;
            double uDo = dRow / (2.0 * sqrt3);   // u(dig0)
            double uDL = dRow / (2.0 * sqrt3);   // u(digL)

            // u(ecc) scales linearly with test load (EURAMET eq. 7.1.3-1a)
            double uEcc = eccMaxDev > 0
                ? eccMaxDev * row.TestLoad / (eccLoad * 2.0 * sqrt3)
                : 0;

            // OIML R111 reference-mass components (MPE-based; replaced by u(cal_ref) when cert U is given)
            double uMB    = 0;
            double uDrift = 0;
            if (uCalRef <= 0 && !string.IsNullOrEmpty(twClass) && row.TestLoad > 0)
            {
                double? mpeMg = DataSheetCalculationService.MassR111Lookup(row.TestLoad, twClass);
                if (mpeMg.HasValue)
                {
                    double mpeRef = mpeMg.Value / 1000.0;   // mg → g
                    uMB    = mpeRef / (4.0 * sqrt3);         // u(mB)    §7.1.2-5c
                    uDrift = mpeRef / (3.0 * sqrt3);         // u(mD)    §7.1.2-11
                }
            }
            double uMconv = uDrift;   // u(mconv) = u(mD)  §7.1.2-13

            // u(mc) — buoyancy from density uncertainty of reference mass (EURAMET cg-18 §7.1.2-7)
            // u(mc) = m_kg × ρ_air × u_std(ρ_ref) / ρ_ref²   [converted back to grams]
            double uMc = row.TestLoad > 0
                ? row.TestLoad * 1.2 * (refDensityHalfWidth / sqrt3) / (refDensityKgM3 * refDensityKgM3)
                : 0;

            double uc = Math.Sqrt(
                uDo * uDo + uDL * uDL +
                uRep * uRep +
                uEcc * uEcc +
                uMB * uMB + uDrift * uDrift + uMconv * uMconv +
                uMc * uMc +
                uCalRef * uCalRef);

            // Welch-Satterthwaite — ν_rep = n - 1 is the only finite contributor
            double vEff = (uRep > 0 && n >= 2)
                ? Math.Pow(uc, 4) * (n - 1) / Math.Pow(uRep, 4)
                : 510.0;
            double k = CoverageFactorFromDof(vEff);

            row.UCombined                 = Math.Round(uc,     6);
            row.UExpanded                 = Math.Round(k * uc, 6);
            row.CoverageFactor            = Math.Round(k,      4);
            row.EffectiveDegreesOfFreedom = Math.Round(vEff,   1);
        }
    }

    // ── NAWI: overall uncertainty budget (certificate budget table) ───────────
    // Evaluated at the maximum linearity test load (conservative).
    public static NawiUncertaintyDto? ComputeNawiUncertaintyBudget(
        NawiRawDataDto raw, NawiCalculatedResultsDto calc, double? e)
    {
        double maxLoad = raw.LinearityRows.Count > 0 ? raw.LinearityRows.Max(r => r.TestLoad) : 0;

        // Select e for the maximum test load (conservative) — respects multi-range configuration
        var    ranges    = raw.InstrumentDetails?.Ranges;
        double? eForBudget = ranges?.Count > 0
            ? (DataSheetCalculationService.ParseScaleInterval(
                  ranges.Where(r => maxLoad <= r.MaxLoad).OrderBy(r => r.MaxLoad).FirstOrDefault()?.Division)
               ?? e)
            : e;

        if (!(eForBudget.HasValue && eForBudget.Value > 0)) return null;

        double sqrt3 = Math.Sqrt(3);
        double eVal  = eForBudget.Value;
        int    n     = raw.Repeatability?.Indications.Count(i => i.HasValue) ?? 0;

        // u(dig0) = u(digL): use actual readability d if provided, else e
        double? dParsed = DataSheetCalculationService.ParseScaleInterval(raw.InstrumentDetails?.ReadabilityDivision);
        double  dVal    = dParsed.HasValue && dParsed.Value > 0 ? dParsed.Value : eVal;
        double  uRes    = dVal / (2.0 * sqrt3);   // u(dig0) = u(digL)

        double? uRepeatability = calc.Repeatability?.StandardDeviation;
        double  uRep           = uRepeatability ?? 0;

        double uEcc = 0;
        if (calc.Eccentricity?.MaximumDeviation.HasValue == true && calc.Eccentricity.TestLoad > 0 && maxLoad > 0)
            uEcc = calc.Eccentricity.MaximumDeviation.Value * maxLoad / (calc.Eccentricity.TestLoad * 2.0 * sqrt3);

        // u(cal_ref) — 8th component; when present, replaces MPE-based reference mass components
        double? certUncG = raw.TestWeights?.CertificateUncertaintyG;
        int     certK    = raw.TestWeights?.CertificateCoverageFactor ?? 2;
        double  uCalRef  = certUncG.HasValue && certUncG.Value > 0
            ? certUncG.Value / certK
            : 0;

        double uMB    = 0;
        double uDrift = 0;
        if (uCalRef <= 0)
        {
            string twClass = DataSheetCalculationService.NormaliseClass(raw.TestWeights?.Class);
            if (!string.IsNullOrEmpty(twClass) && maxLoad > 0)
            {
                double? mpeMg = DataSheetCalculationService.MassR111Lookup(maxLoad, twClass);
                if (mpeMg.HasValue)
                {
                    double mpeRef = mpeMg.Value / 1000.0;
                    uMB    = mpeRef / (4.0 * sqrt3);
                    uDrift = mpeRef / (3.0 * sqrt3);
                }
            }
        }
        double uMconv = uDrift;

        // u(mc) — buoyancy from density uncertainty of reference mass (EURAMET cg-18 §7.1.2-7)
        string  twClass2  = DataSheetCalculationService.NormaliseClass(raw.TestWeights?.Class);
        (double classRho, double classUHalf) = DataSheetCalculationService.GetClassDensity(twClass2);
        double  refRho    = raw.TestWeights?.DensityKgM3            ?? classRho;
        double  refUHalf  = raw.TestWeights?.DensityUncertaintyKgM3 ?? classUHalf;
        double  uMc       = maxLoad > 0
            ? maxLoad * 1.2 * (refUHalf / sqrt3) / (refRho * refRho)
            : 0;

        // u_do and u_dL both contribute — each appears once in the sum
        double uc = Math.Sqrt(
            uRes * uRes + uRes * uRes +
            uRep * uRep +
            uEcc * uEcc +
            uMB * uMB + uDrift * uDrift + uMconv * uMconv +
            uMc * uMc +
            uCalRef * uCalRef);

        double vEff = (uRep > 0 && n >= 2)
            ? Math.Pow(uc, 4) * (n - 1) / Math.Pow(uRep, 4)
            : 510.0;
        double k = CoverageFactorFromDof(vEff);

        return new NawiUncertaintyDto
        {
            UResolution               = Math.Round(uRes,    6),
            URepeatability            = uRepeatability.HasValue ? Math.Round(uRepeatability.Value, 6) : null,
            UEccentricity             = uEcc    > 0 ? Math.Round(uEcc,    6) : null,
            UBuoyancyRefMass          = uMB     > 0 ? Math.Round(uMB,     6) : null,
            UDriftRefMass             = uDrift  > 0 ? Math.Round(uDrift,  6) : null,
            UConvectionRefMass        = uMconv  > 0 ? Math.Round(uMconv,  6) : null,
            UDensityRefMass           = uMc     > 0 ? Math.Round(uMc,     6) : null,
            UCalibrationRef           = uCalRef > 0 ? Math.Round(uCalRef, 6) : null,
            UCombined                 = Math.Round(uc,      6),
            UExpanded                 = Math.Round(k * uc,  6),
            EffectiveDegreesOfFreedom = Math.Round(vEff,    1),
            CoverageFactor            = Math.Round(k,       4),
        };
    }

    // ── Mass standards: uncertainty budget (OIML R 111-1 Annex C.6) ──────────
    //
    // 5 components:
    //   u_bal  = SD / √n                  comparator repeatability   [Type A]
    //   u_res  = d / (2√3)               comparator resolution       [rectangular ±d/2]
    //   u_conv = d / (2√3)               convection                  [rectangular ±d/2]
    //   u_ref  = MPE_R111 / (2√3)        reference standard tolerance [rectangular]
    //   u_buoy = m × ρ_air × u(ρ_t)/ρ_t² air buoyancy               [Type B]
    //
    //   u_c = √(u_bal²+u_res²+u_conv²+u_ref²+u_buoy²)
    //   U   = 2 × u_c   (k = 2, ν_eff typically large for comparator measurements)
    public static MassUncertaintyDto? ComputeMassUncertaintyBudget(
        MassRawDataDto dto, List<MassBlockResultDto> blockResults)
    {
        if (dto.ComparatorDetails is null && dto.MeasurementBlocks.Count == 0) return null;

        double sqrt3 = Math.Sqrt(3);

        // 1. u_bal — pooled SD / √n across all mass readings
        var allMassReadings = dto.MeasurementBlocks.SelectMany(b => new[]
        {
            b.Mass1Ind1, b.Mass1Ind2, b.Mass2Ind1, b.Mass2Ind2
        }).Where(v => v.HasValue).Select(v => v!.Value).ToList();

        double? uBalance = null;
        int nTotal = allMassReadings.Count;
        if (nTotal >= 2)
        {
            double sd = DataSheetCalculationService.SampleStdDev(allMassReadings);
            uBalance  = Math.Round(sd / Math.Sqrt(nTotal), 8);
        }

        // 2. u_res — comparator resolution
        double? comparatorDiv = DataSheetCalculationService.ParseScaleInterval(dto.ComparatorDetails?.Division);
        double? uResolution   = comparatorDiv.HasValue
            ? Math.Round(comparatorDiv.Value / (2.0 * sqrt3), 8)
            : null;

        // 3. u_conv — convection: same as u_res (rectangular ±d/2)
        double? uConvection = uResolution;

        // 4. u_ref — max R111 MPE across all blocks (conservative)
        double? uReference = null;
        foreach (var block in dto.MeasurementBlocks)
        {
            string refClass = DataSheetCalculationService.NormaliseClass(block.ReferenceStdClass);
            if (string.IsNullOrEmpty(refClass)) continue;

            double? nomG = DataSheetCalculationService.ParseNominalGrams(block.NominalValue);
            if (!nomG.HasValue) continue;

            double? mpeMg = DataSheetCalculationService.MassR111Lookup(nomG.Value, refClass);
            if (!mpeMg.HasValue) continue;

            double uRefBlock = (mpeMg.Value / 1000.0) / (2.0 * sqrt3);
            if (!uReference.HasValue || uRefBlock > uReference.Value)
                uReference = uRefBlock;
        }

        // Compute air density: use barometric pressure + T + H when available, else 1.2 kg/m³
        double rhoAirGlobal = ComputeAirDensity(dto.EnvironmentalConditions);

        // 5. u_buoy — at the largest nominal mass (most conservative)
        double? uBuoyancy = null;
        if (dto.MeasurementBlocks.Count > 0)
        {
            double? maxNomG = dto.MeasurementBlocks
                .Select(b => DataSheetCalculationService.ParseNominalGrams(b.NominalValue))
                .Where(v => v.HasValue).Select(v => v!.Value)
                .Cast<double?>().Max();

            if (maxNomG.HasValue)
            {
                const double rhoT = 7950.0;
                double uRhoT = 70.0 / sqrt3;   // u(ρ_t) = 70/√3 kg/m³
                double mKg   = maxNomG.Value / 1000.0;
                uBuoyancy    = Math.Round(mKg * rhoAirGlobal * uRhoT / (rhoT * rhoT) * 1000.0, 8);
            }
        }

        double uBal  = uBalance    ?? 0;
        double uRes  = uResolution ?? 0;
        double uConv = uConvection ?? 0;
        double uRef  = uReference  ?? 0;
        double uBuoy = uBuoyancy   ?? 0;
        double uC    = Math.Sqrt(uBal * uBal + uRes * uRes + uConv * uConv + uRef * uRef + uBuoy * uBuoy);

        // Phase 1B: Welch-Satterthwaite ν_eff — only u_bal is Type A (ν = n−1)
        double vEff = (uBal > 0 && nTotal >= 2)
            ? Math.Pow(uC, 4) * (nTotal - 1) / Math.Pow(uBal, 4)
            : 510.0;
        double k = CoverageFactorFromDof(vEff);

        // Phase 1C + Phase 2: per-block UCombined / UExpanded
        // Uses per-block density, certificate uncertainty, and computed ρ_air
        for (int i = 0; i < blockResults.Count && i < dto.MeasurementBlocks.Count; i++)
        {
            var rawBlock = dto.MeasurementBlocks[i];
            var resBlock = blockResults[i];

            var massReadings = new[] { rawBlock.Mass1Ind1, rawBlock.Mass1Ind2, rawBlock.Mass2Ind1, rawBlock.Mass2Ind2 }
                .Where(v => v.HasValue).Select(v => v!.Value).ToList();

            int nBlock = massReadings.Count;
            double uBalBlock = 0;
            if (nBlock >= 2)
            {
                double sdBlock = DataSheetCalculationService.SampleStdDev(massReadings);
                uBalBlock = sdBlock / Math.Sqrt(nBlock);
            }

            // u_ref per block: use certificate uncertainty if provided, else MPE-based
            double uRefBlk = 0;
            if (rawBlock.ReferenceStdCertificateUncertaintyG.HasValue && rawBlock.ReferenceStdCertificateUncertaintyG.Value > 0)
            {
                uRefBlk = rawBlock.ReferenceStdCertificateUncertaintyG.Value / rawBlock.ReferenceStdCertificateCoverageFactor;
            }
            else
            {
                string refCls = DataSheetCalculationService.NormaliseClass(rawBlock.ReferenceStdClass);
                if (!string.IsNullOrEmpty(refCls))
                {
                    double? nomG  = DataSheetCalculationService.ParseNominalGrams(rawBlock.NominalValue);
                    double? mpeMg = nomG.HasValue ? DataSheetCalculationService.MassR111Lookup(nomG.Value, refCls) : null;
                    if (mpeMg.HasValue)
                        uRefBlk = (mpeMg.Value / 1000.0) / (2.0 * sqrt3);
                }
            }

            // u_buoy per block: use per-block density, computed ρ_air
            double uBuoyBlk = 0;
            double? nomGBlk = DataSheetCalculationService.ParseNominalGrams(rawBlock.NominalValue);
            if (nomGBlk.HasValue)
            {
                double rhoT  = rawBlock.ReferenceStdDensityKgM3 ?? 7950.0;
                double uRhoT = 70.0 / sqrt3;
                uBuoyBlk = nomGBlk.Value / 1000.0 * rhoAirGlobal * uRhoT / (rhoT * rhoT) * 1000.0;
            }

            double ucBlk = Math.Sqrt(
                uBalBlock * uBalBlock +
                uRes * uRes + uConv * uConv +
                uRefBlk * uRefBlk +
                uBuoyBlk * uBuoyBlk);

            double vEffBlk = (uBalBlock > 0 && nBlock >= 2)
                ? Math.Pow(ucBlk, 4) * (nBlock - 1) / Math.Pow(uBalBlock, 4)
                : 510.0;
            double kBlk = CoverageFactorFromDof(vEffBlk);

            resBlock.UCombined = Math.Round(ucBlk,        8);
            resBlock.UExpanded = Math.Round(kBlk * ucBlk, 8);
        }

        return new MassUncertaintyDto
        {
            UBalance                  = uBalance   .HasValue ? Math.Round(uBalance.Value,    8) : null,
            UResolution               = uResolution.HasValue ? Math.Round(uResolution.Value, 8) : null,
            UConvection               = uConvection.HasValue ? Math.Round(uConvection.Value, 8) : null,
            UReferenceWeight          = uReference .HasValue ? Math.Round(uReference.Value,  8) : null,
            UAirBuoyancy              = uBuoyancy  .HasValue ? Math.Round(uBuoyancy.Value,   8) : null,
            UCombined                 = Math.Round(uC,        8),
            UExpanded                 = Math.Round(k * uC,    8),
            EffectiveDegreesOfFreedom = Math.Round(vEff,      1),
            CoverageFactor            = Math.Round(k,         4),
        };
    }

    // ── Air density (BIPM formula) ────────────────────────────────────────────
    // ρ_air = 0.0034848 × P_Pa / (273.15 + T) × (1 − 0.378 × e_w_Pa / P_Pa)
    // Input P is stored in hPa; convert to Pa (×100) before applying the constant.
    // Magnus formula gives e_w in hPa; likewise converted to Pa.
    // Falls back to 1.2 kg/m³ when barometric pressure is not captured.
    public static double ComputeAirDensity(EnvironmentalConditionsDto? env)
    {
        const double defaultRhoAir = 1.2;
        if (env?.BarometricPressureHPa is not { } pHPa) return defaultRhoAir;

        double t    = ((env.StartTemperature ?? 20) + (env.EndTemperature ?? env.StartTemperature ?? 20)) / 2.0;
        double h    = ((env.StartHumidity   ?? 50) + (env.EndHumidity    ?? env.StartHumidity    ?? 50)) / 2.0;
        double ewHPa = 6.1078 * Math.Pow(10, 7.5 * t / (237.3 + t)) * (h / 100.0);
        // Convert hPa → Pa for the BIPM constant (3.4848 × 10⁻³ kg·Pa⁻¹·m⁻³·K)
        double pPa   = pHPa   * 100.0;
        double ewPa  = ewHPa  * 100.0;
        return 0.0034848 * pPa / (273.15 + t) * (1.0 - 0.378 * ewPa / pPa);
    }

    // ── GUM Table G.2: coverage factor at p = 95.45 % ────────────────────────
    // For ν_eff ≥ 30 the t-distribution converges to the normal: k = 2.000.
    // Typical lab scenario (n_rep ≥ 10): ν_eff ≫ 30, so k = 2.000 always.
    public static double CoverageFactorFromDof(double vEff)
    {
        if (vEff >= 30) return 2.000;
        if (vEff >= 20) return 2.086;
        if (vEff >= 15) return 2.131;
        if (vEff >= 10) return 2.228;
        if (vEff >=  9) return 2.262;
        if (vEff >=  8) return 2.306;
        if (vEff >=  7) return 2.365;
        if (vEff >=  6) return 2.447;
        if (vEff >=  5) return 2.571;
        if (vEff >=  4) return 2.776;
        if (vEff >=  3) return 3.182;
        if (vEff >=  2) return 4.303;
        return 12.706;   // ν = 1
    }
}
