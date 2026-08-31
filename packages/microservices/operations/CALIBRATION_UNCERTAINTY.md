# Calibration Uncertainty — Procedure, Formulas & Variables

Reference standards: **EURAMET cg-18 v4.0** (NAWI) · **OIML R 111-1** (mass) · **GUM JCGM 100:2008**

Legend: ✅ captured · ⚠️ assumed / hardcoded · ❌ not captured (DTO gap)

---

## 1. Architecture: two services, one calculation chain

```
Raw data (JSON)
      │
      ▼
DataSheetCalculationService          ← test results only
  • eccentricity errors & pass/fail
  • repeatability errors, SD, pass/fail
  • linearity errors (as-found / definitive) & pass/fail
  • mass ABBA substitution, Δ, buoyancy correction value
      │
      ▼
CertificateCalculationService        ← uncertainty budget
  • ComputeAirDensity()              — BIPM formula (P, T, H → ρ_air kg/m³)
  • EnrichLinearityWithUncertainty() — fills every linearity row with U, k, ν_eff
  • ComputeNawiUncertaintyBudget()   — overall NAWI budget (evaluated at max test load)
  • ComputeMassUncertaintyBudget()   — mass standards budget
      │
      ▼
CalculatedResultsJson stored on LabDataSheet entity
```

> **Stale stored JSON:** if code changes after a sheet was submitted, the stored JSON will be
> outdated. Hit **Recalculate** (always visible on submitted NAWI sheets) to recompute with
> current formulas. Budget U and per-row U at max load should always match.

---

## 2. Complete variable dictionary

### 2.1 Instrument variables

| Symbol | Full name | Status | Source / assumption |
|--------|-----------|--------|---------------------|
| `e` | Verification scale interval (scale division for MPE) | ✅ | `InstrumentDetails.Division` — parsed by `ParseScaleInterval()` |
| `d` | Actual display readability (may be finer than `e`) | ✅ | `InstrumentDetails.ReadabilityDivision` — falls back to `e` when absent |
| `Max` | Maximum capacity | ✅ | `InstrumentDetails.MaximumCapacity` |
| `Min` | Minimum capacity | ✅ | `InstrumentDetails.MinimumCapacity` |
| Class | Accuracy class (I / II / III / IIII) | ✅ | `InstrumentDetails.AccuracyClass` → `NormaliseClass()` |
| Ranges | Multi-range e table | ✅ | `InstrumentDetails.Ranges` — list of `(MaxLoad, Division)` pairs; `GetEForLoad()` selects the correct `e` per test load |

---

### 2.2 Reference (test) weight variables

| Symbol | Full name | Status | Source / assumption |
|--------|-----------|--------|---------------------|
| `Class_ref` | OIML weight class (E1–M3) | ✅ | `TestWeights.Class` |
| `MPE_R111(L, Class_ref)` | Max permissible error at load L from OIML R 111-1 Table 1 (mg) | ✅ | `MassR111Lookup(grams, class)` |
| `U_cert` | Expanded uncertainty from the reference weight's own calibration certificate | ✅ | `TestWeights.CertificateUncertaintyG` (grams). When provided, replaces MPE-based u(mB)/u(mD)/u(mconv) with u(cal_ref) |
| `k_cert` | Coverage factor on the reference weight certificate | ✅ | `TestWeights.CertificateCoverageFactor` (default 2) |
| `ρ_ref` | Density of the reference weights (kg/m³) | ⚠️ | **Hardcoded 7 950 kg/m³** (stainless steel). Per-block override via `MassMeasurementBlockDto.ReferenceStdDensityKgM3` (Mass only) |

---

### 2.3 Environmental variables

| Symbol | Full name | Status | Source / assumption |
|--------|-----------|--------|---------------------|
| `T_start` | Temperature at start of test (°C) | ✅ | `EnvironmentalConditions.StartTemperature` |
| `T_end` | Temperature at end of test (°C) | ✅ | `EnvironmentalConditions.EndTemperature` |
| `H_start` | Relative humidity at start (%) | ✅ | `EnvironmentalConditions.StartHumidity` |
| `H_end` | Relative humidity at end (%) | ✅ | `EnvironmentalConditions.EndHumidity` |
| `P` | Barometric pressure (hPa / mbar) | ✅ | `EnvironmentalConditions.BarometricPressureHPa` — when absent, ρ_air falls back to 1.2 kg/m³ |
| `ρ_air` | Air density (kg/m³) | ✅ | Computed via BIPM formula (see §2.4). Fallback 1.2 kg/m³ when P not captured |
| `ρ_t` | Density of the item being weighed / reference mass (kg/m³) | ⚠️ | **Hardcoded 7 950 kg/m³** for NAWI. Per-block `ReferenceStdDensityKgM3` for Mass |
| `u(ρ_t)` | Standard uncertainty of the density of reference material | ⚠️ | **Hardcoded 70/√3 kg/m³** |

---

### 2.4 BIPM air density formula

```
ρ_air = 0.0034848 × P_Pa / (273.15 + T_avg) × (1 − 0.378 × e_w_Pa / P_Pa)

where:
  P_Pa   = BarometricPressureHPa × 100        ← CRITICAL: constant needs Pa, not hPa
  T_avg  = (T_start + T_end) / 2  (°C)
  e_w    = Magnus formula water vapour pressure
         = 6.1078 × 10^(7.5 × T_avg / (237.3 + T_avg)) × (H_avg / 100)   [hPa]
  e_w_Pa = e_w × 100
  H_avg  = (H_start + H_end) / 2  (%)
```

At standard conditions (P = 1013.25 hPa, T = 20 °C, H = 50 %) → ρ_air ≈ 1.204 kg/m³.

**Pitfall:** applying the constant 0.0034848 with P in hPa gives ~0.012 kg/m³ (100× too small).

---

### 2.5 Measurement variables

| Symbol | Full name | Status | Source |
|--------|-----------|--------|--------|
| `L` | Test load at current linearity row | ✅ | `LinearityRows[n].TestLoad` (grams) |
| `L_ecc` | Eccentricity test load | ✅ | `Eccentricity.TestLoad` |
| `ΔI_ecc_max` | Max absolute deviation from centre in eccentricity test | ✅ | Computed: max(\|Ind_i − Ind_1\|) for i = 2..5 |
| `n_rep` | Number of valid repeatability readings | ✅ | `Repeatability.Indications.Count(v => v != null)` |
| `s_r` | Sample standard deviation of repeatability indications | ✅ | `SampleStdDev(Indications)` |
| `ν_rep` | Degrees of freedom for repeatability | ✅ | `n_rep − 1` |

---

## 3. NAWI uncertainty model — 7 + 1 components

Per EURAMET cg-18 §7.1.3-1a, evaluated **per linearity test load L**:

```
┌─────────────┬──────────────────────────────────────────┬────────────────────────────────┐
│  Component  │  Formula                                 │  Notes                         │
├─────────────┼──────────────────────────────────────────┼────────────────────────────────┤
│ u(dig0)     │  d / (2√3)                               │  resolution, no-load reading   │
│ u(digL)     │  d / (2√3)                               │  resolution, at-load reading   │
│ u(rep)      │  s_r                                     │  Type A, ν = n_rep − 1         │
│ u(ecc)      │  ΔI_ecc_max × L / (L_ecc × 2√3)         │  scales with load; rectangular │
│ u(mB)       │  MPE_R111(L, Class_ref) / (4√3)          │  buoyancy of ref. mass §7.1.2-5c│
│ u(mD)       │  MPE_R111(L, Class_ref) / (3√3)          │  drift of ref. mass §7.1.2-11  │
│ u(mconv)    │  u(mD)                                   │  convection §7.1.2-13          │
│ u(cal_ref)  │  U_cert / k_cert                         │  8th component — only when     │
│             │                                          │  TestWeights.CertificateUncertaintyG│
│             │                                          │  is provided; replaces u(mB)/  │
│             │                                          │  u(mD)/u(mconv) when non-zero  │
└─────────────┴──────────────────────────────────────────┴────────────────────────────────┘

  u_c(L) = √( u(dig0)² + u(digL)² + u(rep)² + u(ecc)² + u(mB)² + u(mD)² + u(mconv)² + u(cal_ref)² )
```

For a single-interval instrument where d = e, the two resolution terms are equal:

```
  u_c(L) = √( 2·[e/(2√3)]² + s_r² + u(ecc)² + u(mB)² + u(mD)² + u(mconv)² + u(cal_ref)² )
         = √( e²/6  +  s_r²  +  u(ecc)²  +  u(mB)²  +  u(mD)²  +  u(mconv)²  +  u(cal_ref)² )
```

---

## 3.1 How each formula is derived

### u(dig0) and u(digL) — resolution

A digital display with scale division `d` can only show values in multiples of `d`.
The true value lies anywhere in a ±d/2 window → rectangular distribution:

```
  standard uncertainty = half-width / √3 = (d/2) / √3 = d / (2√3)
```

We apply this twice: once for the no-load reading (zero), once for the loaded reading.
Uses `d` (readability) if `InstrumentDetails.ReadabilityDivision` is set, otherwise falls back to `e`.

### u(rep) — repeatability

Pure Type A evaluation. Take n readings at the same load, compute sample SD:

```
  s_r = √( Σ(Iᵢ − Ī)² / (n−1) )     ← SampleStdDev()
  u(rep) = s_r
  ν_rep = n − 1
```

### u(ecc) — eccentricity

When the test load is placed off-centre it produces a different reading. The uncertainty
in actual use is bounded by the worst observed offset error, scaled by load:

```
  u(ecc)(L) = ΔI_ecc_max × L / (L_ecc × 2√3)

  ΔI_ecc_max = max( |I_pos2 − I_centre|, |I_pos3 − I_centre|, |I_pos4 − I_centre|, |I_pos5 − I_centre| )
```

The factor `L / L_ecc` scales the eccentricity error linearly to the current test load.
The `/2√3` converts from half-width of a rectangular distribution.

**Important:** `u(ecc)` grows with load. The per-row U in the linearity table increases
towards maximum capacity. The overall budget U must be evaluated at the maximum test load
(most conservative) and should match the per-row U at that load.

### u(mB) — buoyancy of reference mass (EURAMET §7.1.2-5c)

```
  Upper bound on buoyancy correction uncertainty: δm_B ≤ MPE_R111 / 2
  Rectangular distribution → u = (MPE/2) / (2√3) = MPE / (4√3)
```

### u(mD) — drift of reference mass (EURAMET §7.1.2-11)

```
  Drift estimate: D ≈ MPE_R111 / 3
  Rectangular distribution → u(mD) = D / √3 = MPE_R111 / (3√3)
```

### u(mconv) — convection (EURAMET §7.1.2-13)

```
  u(mconv) = u(mD) = MPE_R111 / (3√3)
```

### u(cal_ref) — reference weight calibration certificate (8th component)

When the reference weight has been calibrated and the certificate states an expanded
uncertainty `U_cert` at coverage factor `k_cert`:

```
  u(cal_ref) = U_cert / k_cert

  → stored in TestWeights.CertificateUncertaintyG (grams) and CertificateCoverageFactor
  → when u(cal_ref) > 0 the MPE-based terms u(mB), u(mD), u(mconv) are replaced by it
```

---

## 4. Welch-Satterthwaite effective degrees of freedom (GUM App. B3)

```
              u_c(L)⁴ · ν_rep
ν_eff   =  ─────────────────────    where  ν_rep = n_rep − 1
                u(rep)⁴
```

All Type B components (dig0, digL, ecc, mB, mD, mconv, cal_ref) have ν = ∞, so they
vanish from the denominator. Only u(rep) (Type A) contributes a finite term.

When `s_r = 0` (all readings identical): assign `ν_eff = 510` → k ≈ 2.005 ≈ 2.000.

---

## 5. Coverage factor k (GUM Table G.2, p = 95.45 %)

```
  ν_eff ≥ 30  →  k = 2.000     (converges to normal distribution)
```

Full table used by `CoverageFactorFromDof()`:

| ν_eff | k      |  | ν_eff | k      |
|------:|:-------|--|------:|:-------|
| ≥ 30  | 2.000  |  |   6   | 2.447  |
|  20   | 2.086  |  |   5   | 2.571  |
|  15   | 2.131  |  |   4   | 2.776  |
|  10   | 2.228  |  |   3   | 3.182  |
|   9   | 2.262  |  |   2   | 4.303  |
|   8   | 2.306  |  |   1   | 12.706 |
|   7   | 2.365  |  |       |        |

For `n_rep ≥ 10` readings, `ν_eff` will almost always be >> 30 → **k = 2.000**.

---

## 6. Building the two certificate columns — step-by-step

The certificate linearity table, one row per test load:

```
┌──────────┬──────────────┬──────────────┬────────┬──────────┬─────────────────────┐
│ Test load│ As-found err │ Definitive   │  MPE   │Coverage  │ Expanded uncertainty│
│    L     │              │ error        │        │factor k  │        U            │
├──────────┼──────────────┼──────────────┼────────┼──────────┼─────────────────────┤
│  2 000 g │   +0.2 g     │   +0.1 g     │ ±1.0 g │  2.000   │       0.032 g       │
│  5 000 g │   +0.3 g     │   +0.2 g     │ ±1.0 g │  2.000   │       0.042 g       │
│ 10 000 g │   +0.5 g     │   +0.4 g     │ ±1.5 g │  2.000   │       0.058 g       │
└──────────┴──────────────┴──────────────┴────────┴──────────┴─────────────────────┘
```

**For every row (test load L):**

```
① Instrument properties
  e    = ParseScaleInterval(InstrumentDetails.Division)      e.g. "2 g" → 2.0
  d    = ParseScaleInterval(InstrumentDetails.ReadabilityDivision) ?? e
  class = NormaliseClass(InstrumentDetails.AccuracyClass)    e.g. "III"
  e    = GetEForLoad(L, Ranges, e)                           per-range override

② Error and pass/fail  [DataSheetCalculationService]
  AsFoundError    = AsFoundIndication − L
  DefinitiveError = DefinitiveIndication − L
  MPE             = NawiMpe(L, e, class)                     OIML R 76-1 Table 6
  Pass            = |DefinitiveError| ≤ MPE

③ Resolution components  [CertificateCalculationService]
  u(dig0) = d / (2√3)
  u(digL) = d / (2√3)

④ Repeatability component
  u(rep) = s_r                ← from RepeatabilityResults.StandardDeviation

⑤ Eccentricity component
  u(ecc) = ΔI_ecc_max × L / (L_ecc × 2√3)

⑥ Reference mass components (MPE-based when no certificate U provided)
  mpe_ref   = MassR111Lookup(L, Class_ref) / 1000   [mg → g]
  u(mB)     = mpe_ref / (4√3)
  u(mD)     = mpe_ref / (3√3)
  u(mconv)  = u(mD)

  OR (when TestWeights.CertificateUncertaintyG is set):
  u(cal_ref) = CertificateUncertaintyG / CertificateCoverageFactor
  u(mB) = u(mD) = u(mconv) = 0     ← replaced by u(cal_ref)

⑦ Combined standard uncertainty
  u_c = √( u(dig0)² + u(digL)² + u(rep)² + u(ecc)² + u(mB)² + u(mD)² + u(mconv)² + u(cal_ref)² )

⑧ Degrees of freedom and coverage factor
  ν_eff = u_c⁴ × (n_rep − 1) / u(rep)⁴     [or 510 when u(rep)=0]
  k     = CoverageFactorFromDof(ν_eff)        GUM Table G.2, 95.45 %

⑨ Certificate columns
  CoverageFactor  = k
  U               = k × u_c
```

---

## 7. Overall NAWI uncertainty budget table (certificate budget section)

`ComputeNawiUncertaintyBudget()` evaluates the same components at the **maximum**
linearity test load (conservative). Stored in `NawiCalculatedResultsDto.Uncertainty`.

| Component | Symbol | Formula | DTO field |
|-----------|--------|---------|-----------|
| Resolution (×2 terms) | u(dig0) = u(digL) | d / (2√3) | `UResolution` |
| Repeatability | u(rep) | s_r | `URepeatability` |
| Eccentricity | u(ecc) | ΔI_max × L_max / (L_ecc × 2√3) | `UEccentricity` |
| Buoyancy of ref. mass | u(mB) | MPE_R111 / (4√3) | `UBuoyancyRefMass` |
| Drift of ref. mass | u(mD) | MPE_R111 / (3√3) | `UDriftRefMass` |
| Convection | u(mconv) | = u(mD) | `UConvectionRefMass` |
| Ref. weight certificate | u(cal_ref) | U_cert / k_cert | `UCalibrationRef` |
| **Combined** | **u_c** | **√(Σuᵢ²)** | `UCombined` |
| **Expanded** | **U** | **k × u_c** | `UExpanded` |
| Eff. degrees of freedom | ν_eff | W-S formula | `EffectiveDegreesOfFreedom` |
| Coverage factor | k | GUM Table G.2, 95.45 % | `CoverageFactor` |

> **Consistency check:** `UExpanded` in the budget must equal the per-row `UExpanded` at
> the maximum linearity test load. If they differ, the stored JSON is stale — recalculate.

---

## 8. OIML R 76-1 MPE table used for pass/fail

`NawiMpe(L, e, class)` returns the maximum permissible error at load L:

```
  MPE = MpeFactor(L/e, class) × e

  MpeFactor lookup (OIML R 76-1 Table 6, initial verification):

  Class I   │  L/e ≤ 50 000: ±0.5e │  ≤ 200 000: ±1.0e │  > 200 000: ±1.5e
  Class II  │  L/e ≤  5 000: ±0.5e │  ≤  20 000: ±1.0e │  >  20 000: ±1.5e
  Class III │  L/e ≤    500: ±0.5e │  ≤   2 000: ±1.0e │  >   2 000: ±1.5e
  Class IIII│  L/e ≤     50: ±0.5e │  ≤     200: ±1.0e │  >     200: ±1.5e
```

---

## 9. Mass standards model (OIML R 111-1 Annex C.6)

### 9.1 Uncertainty budget — 5 components

```
┌──────────┬─────────────────────────────────────────────┬───────────────────────────┐
│ Symbol   │ Formula                                     │ Notes                     │
├──────────┼─────────────────────────────────────────────┼───────────────────────────┤
│ u_bal    │ SD_comparator / √n                          │ Type A, ν = n−1           │
│ u_res    │ d / (2√3)                                   │ comparator scale division │
│ u_conv   │ d / (2√3)  [= u_res]                        │ convection disturbance    │
│ u_ref    │ MPE_R111(m_ref, Class_ref) / (2√3)          │ reference std tolerance   │
│          │  OR: ReferenceStdCertificateUncertaintyG    │ when cert U is provided   │
│          │       / ReferenceStdCertificateCoverageFactor│                          │
│ u_buoy   │ m × ρ_air × u(ρ_t) / ρ_t²   × 1000        │ result in grams           │
│          │   ρ_air = BIPM formula (§2.4); fallback 1.2 │                           │
│          │   ρ_t   = ReferenceStdDensityKgM3 ?? 7950   │ per-block override ✅     │
│          │   u(ρ_t)= 70/√3 kg/m³ ⚠️ hardcoded          │                           │
└──────────┴─────────────────────────────────────────────┴───────────────────────────┘

  u_c = √( u_bal² + u_res² + u_conv² + u_ref² + u_buoy² )
  U   = k × u_c      (k from Welch-Satterthwaite, same pattern as NAWI)
```

DTO fields: `MassUncertaintyDto.UBalance`, `UResolution`, `UConvection`, `UReferenceWeight`, `UAirBuoyancy`.

> **Note:** `UReferenceWeight` is a **Mass-only** field. Never use it to render a NAWI
> uncertainty section on the frontend — NAWI uses `UBuoyancyRefMass`, `UDriftRefMass`, etc.

### 9.2 Buoyancy correction value (per block)

Separate from the uncertainty, this is a **correction to the measured mass difference**:

```
  C_buoy = m_kg × ρ_air × (1/ρ_item − 1/ρ_ref) × 10⁶   [mg]

  where:
    m_kg    = NominalValue (grams) / 1000
    ρ_air   = ComputeAirDensity(EnvironmentalConditions)
    ρ_item  = MassMeasurementBlockDto.ItemDensityKgM3     (required to compute)
    ρ_ref   = MassMeasurementBlockDto.ReferenceStdDensityKgM3 ?? 7950

  FinalCorrectionMg = EffectiveCorrectionMg + C_buoy
```

Stored in `MassBlockResultDto.BuoyancyCorrectionMg` and `FinalCorrectionMg`.

---

## 10. OIML R 111-1 Table 1 lookup

`MassR111Lookup(grams, class)` → MPE in **milligrams**.  Table coverage: 0.001 g – 5 000 kg, classes E1–M3.

Selected rows (full table in `DataSheetCalculationService.cs`):

| Nominal (g) | E1 (mg) | E2 (mg) | F1 (mg) | F2 (mg) | M1 (mg) | M2 (mg) | M3 (mg) |
|------------:|--------:|--------:|--------:|--------:|--------:|--------:|--------:|
| 100 000 | — | 160 | 500 | 1 600 | 5 000 | 16 000 | 50 000 |
| 10 000  | 5.0 | 16 | 50 | 160 | 500 | 1 600 | 5 000 |
| 1 000   | 0.5 | 1.6 | 5.0 | 16 | 50 | 160 | 500 |
| 100     | 0.05 | 0.16 | 0.5 | 1.6 | 5.0 | 16 | 50 |
| 10      | 0.020 | 0.06 | 0.20 | 0.6 | 2.0 | 6.0 | 20 |
| 1       | 0.010 | 0.03 | 0.10 | 0.3 | 1.0 | 3.0 | 10 |

For NAWI: nominal value = the test load converted to grams.
For loads above 5 000 kg the table has no entry → `u(mB)`, `u(mD)`, `u(mconv)` default to **zero**.

---

## 11. Current gaps

| Gap | Impact | Status |
|-----|--------|--------|
| `ρ_ref` hardcoded 7 950 kg/m³ for NAWI | Small density error for non-stainless test weights | ⚠️ Still hardcoded for NAWI; Mass has per-block override |
| `u(ρ_t)` hardcoded 70/√3 kg/m³ | Fixed assumed density uncertainty | ⚠️ Low priority |
| Environmental conditions captured but unused for NAWI u | Temperature influence not propagated | ❌ No u(T) component implemented |

All other previously listed gaps (barometric pressure, `d` vs `e`, multi-range, `U_cert`, BIPM formula) are now **implemented**.

---

## 12. Worked example — 100 g load, Class III, e = 0.1 g, M2 test weights

This matches the instrument in the sample certificate (BWS, max 300 g, e = 0.1 g).

```
Inputs
  e            = 0.1 g          (InstrumentDetails.Division = "0.1")
  d            = 0.1 g          (no separate ReadabilityDivision → d = e)
  accuracy     = III
  L            = 100 g          (LinearityRows[n].TestLoad)
  n_rep        = 5              (5 repeatability readings)
  s_r          = 0 g            (all 5 readings identical → SD = 0)
  ΔI_ecc_max   = 0.1 g          (max deviation from centre)
  L_ecc        = 60 g           (eccentricity test load)
  Class_ref    = M2
  MPE_R111(100 g, M2) = 16 mg = 0.016 g   (MassR111Lookup, row g=100, M2 column)

Calculation  (√3 = 1.7321)
  u(dig0)  =  0.1 / (2 × 1.7321)           =  0.028868 g
  u(digL)  =  0.1 / (2 × 1.7321)           =  0.028868 g
  u(rep)   =  0 g
  u(ecc)   =  0.1 × 100 / (60 × 2 × 1.7321)
           =  10 / 207.85                   =  0.048113 g
  u(mB)    =  0.016 / (4 × 1.7321)         =  0.002309 g
  u(mD)    =  0.016 / (3 × 1.7321)         =  0.003079 g
  u(mconv) =  0.003079 g

  u_c = √( 0.028868² + 0.028868² + 0² + 0.048113² + 0.002309² + 0.003079² + 0.003079² )
      = √( 0.000833 + 0.000833 + 0 + 0.002315 + 0.00000533 + 0.00000948 + 0.00000948 )
      = √( 0.003995 )
      = 0.063206 g

  ν_eff = u_c⁴ × (n_rep − 1) / u(rep)⁴
        = any / 0  →  set to 510  (u(rep) = 0)
        → k = 2.000  (ν_eff >> 30)

  U = 2.000 × 0.063206 = 0.126412 g   ≈ ±0.126 g

Certificate columns:
  Coverage factor k                =  2.0
  Expanded measurement uncertainty =  ±0.126 g
```

This matches the `±0.126582 g` shown in the sample certificate at 100 g (minor rounding diff).

---

## 13. Notes on rounding and significant figures

- All intermediate values computed at full `double` precision (64-bit IEEE 754).
- `UCombined` and `UExpanded` on linearity rows: **6 decimal places**.
- `UCombined` and `UExpanded` on budget (`NawiUncertaintyDto`): **6 decimal places**.
- Mass uncertainty components: **8 decimal places**.
- `CoverageFactor`: **4 decimal places** (e.g. `2.0000`).
- `EffectiveDegreesOfFreedom`: **1 decimal place**.
- Certificate PDF renderer applies final display rounding (typically 3–6 sig. figs. for U).
