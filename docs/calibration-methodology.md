# Calibration Methodology: Non-Automatic Weighing Instruments & Mass Standards

**Lante ERP — Technical Reference**
**Standard references:** OIML R 76-1 (NAWI), OIML R 111-1 (Mass), EURAMET cg-18 v4.0 (Uncertainty)

---

## 1. Overview

This paper documents the end-to-end calibration workflow implemented in the Lante system, from instrument intake through test execution, error calculation, uncertainty evaluation, and certificate generation. Two instrument types are covered:

- **Non-Automatic Weighing Instruments (NAWI)** — balances and weighbridges, calibrated against OIML R 76-1
- **Mass Standards** — weights calibrated by comparison, following OIML R 111-1 with ABBA substitution

---

## 2. Workflow Overview

```
Service Request
      │
      ▼
Ticket Created (Ticketing Service)
      │  (department assignment)
      ▼
Assignment Created (Operations Service)
      │  (manager assigns technician)
      ▼
Lab Work Order Opened
      │
      ├── Intake Form  — instrument condition, environmental conditions at receipt
      ├── Bench Work   — technician records test readings (raw data sheet)
      │
      ▼
Data Sheet Submitted
      │  POST /lab-work-order/data-sheet
      │  → CalculateNawi() or CalculateMass() runs immediately
      ▼
Calculated Results stored as JSON
      │  (errors, deviations, pass/fail, uncertainty components)
      ▼
Technician Review → Manager / TM Review
      │
      ▼
Certificate Generated
      │  POST /lab-work-order/generate-certificate
      │  → CertificateNumber assigned, PDF rendered (QuestPDF)
      ▼
Certificate Issued & Dispatched
```

---

## 3. NAWI Calibration (OIML R 76-1)

### 3.1 Instrument Parameters

| Parameter | Symbol | Notes |
|---|---|---|
| Scale interval | *e* | Verification scale interval (g) |
| Actual readability | *d* | Display scale interval; *d* = *e* when not separately stated |
| Accuracy class | — | I, II, III, or IIII |
| Maximum capacity | Max | Largest load the instrument is designed to weigh |
| Minimum capacity | Min | Smallest load; compared against OIML R 76-1 Table 1 |

Multi-range instruments store a list of `(MaxLoad, Division)` pairs; the applicable *e* for each test load is selected as the scale interval of the first range whose `MaxLoad ≥ testLoad`.

---

### 3.2 Maximum Permissible Error (MPE)

MPE on initial verification is taken from **OIML R 76-1 Table 6**. It depends on the test load expressed as a multiple of *e* and on the accuracy class:

| Class | Load range (× *e*) | MPE |
|---|---|---|
| I | ≤ 50 000 | ±0.5 *e* |
| I | 50 001 – 200 000 | ±1.0 *e* |
| I | > 200 000 | ±1.5 *e* |
| II | ≤ 5 000 | ±0.5 *e* |
| II | 5 001 – 20 000 | ±1.0 *e* |
| II | > 20 000 | ±1.5 *e* |
| III | ≤ 500 | ±0.5 *e* |
| III | 501 – 2 000 | ±1.0 *e* |
| III | > 2 000 | ±1.5 *e* |
| IIII | ≤ 50 | ±0.5 *e* |
| IIII | 51 – 200 | ±1.0 *e* |
| IIII | > 200 | ±1.5 *e* |

$$\text{MPE}(L) = f\!\left(\frac{L}{e},\,\text{class}\right)\times e$$

---

### 3.3 Eccentricity Test

#### Purpose
Verifies that the instrument reading does not change unacceptably when a load is placed at different positions on the load receptor (OIML R 76-1 §3.6.2).

#### Test load requirement
The eccentricity test load shall be at least one-third of the maximum capacity:

$$L_{\text{ecc}} \geq \frac{\text{Max}}{3}$$

#### Error calculation

Three loading patterns are supported:

**Radial (balance, 5-position):**
Centre (position 1) is the reference. Error at each off-centre position:

$$\delta_i = I_i - I_{\text{centre}}, \quad i = 2, 3, 4, 5$$

**End-to-End (weighbridge, 2-position):**

$$\delta = I_{\text{end}} - I_{\text{first\,end}}$$

**End-Middle-End (weighbridge, 3-position):**
Middle is the reference:

$$\delta_{\text{front}} = I_{\text{front}} - I_{\text{middle}}, \quad \delta_{\text{back}} = I_{\text{back}} - I_{\text{middle}}$$

#### Pass criterion

$$|\delta_{\max}| \leq \text{MPE}(L_{\text{ecc}})$$

where $|\delta_{\max}| = \max_i |\delta_i|$.

---

### 3.4 Repeatability Test

#### Purpose
Evaluates the ability of the instrument to provide the same result under repeated application of the same load (OIML R 76-1 §3.6.1).

Minimum number of readings: **5** for balances, **3** for weighbridges.

#### Error for each reading *n*

$$\varepsilon_n = I_n - L_{\text{rep}}$$

#### Standard deviation (sample)

$$s_r = \sqrt{\frac{\sum_{n=1}^{N}(I_n - \bar{I})^2}{N - 1}}$$

#### Pass criterion

$$\max_n |\varepsilon_n| \leq \text{MPE}(L_{\text{rep}})$$

---

### 3.5 Discrimination Test

#### Purpose
Verifies that the instrument responds to a small change in load (OIML R 76-1 §4.4).

#### Procedure
With a load on the pan, an additional weight of **1.4 × d** is added. The indication must change.

#### Pass criterion

$$|I_2 - I_1| \geq d$$

where *d* is the actual display readability.

---

### 3.6 Linearity (Accuracy) Test

#### Purpose
Checks that errors at each test load across the full range do not exceed the MPE (OIML R 76-1 §3.6.1).

#### Error calculation

For each test load *L*:

$$E_{\text{as-found}} = I_{\text{as-found}} - L$$

$$E_{\text{definitive}} = I_{\text{definitive}} - L$$

"As-found" is the indication before any adjustment. "Definitive" is the indication used for the certificate (after adjustment if performed).

#### Pass criterion (per row)

$$|E_{\text{definitive}}| \leq \text{MPE}(L)$$

#### Minimum capacity check

OIML R 76-1 Table 1 requires the minimum capacity to be at least a certain multiple of *e*:

| Class | Min capacity |
|---|---|
| I | 100 *e* |
| II, III | 20 *e* |
| IIII | 10 *e* |

---

## 4. NAWI Measurement Uncertainty (EURAMET cg-18 v4.0)

Uncertainty is evaluated per the **GUM framework** (JCGM 100:2008) using the seven-component model from EURAMET cg-18 §7.1.3-1a. The uncertainty is computed both **per linearity row** (certificate table) and as a **single overall budget** (evaluated conservatively at the maximum test load).

### 4.1 Uncertainty Components

| Component | Symbol | Formula | Distribution | Source |
|---|---|---|---|---|
| Resolution at no-load | $u(\text{dig}_0)$ | $d / (2\sqrt{3})$ | Rectangular ±*d*/2 | EURAMET §7.1.1 |
| Resolution at load *L* | $u(\text{dig}_L)$ | $d / (2\sqrt{3})$ | Rectangular ±*d*/2 | EURAMET §7.1.1 |
| Repeatability | $u(\text{rep})$ | $s_r$ | Normal (Type A) | §7.1.3-1a |
| Eccentricity at load *L* | $u(\text{ecc})$ | $\delta_{\max} \cdot L \,/\, (L_{\text{ecc}} \cdot 2\sqrt{3})$ | Rectangular | §7.1.3-1a |
| Buoyancy of ref. mass | $u(m_B)$ | $\text{MPE}_{\text{R111}} / (4\sqrt{3})$ | Rectangular | §7.1.2-5c |
| Drift of ref. mass | $u(m_D)$ | $\text{MPE}_{\text{R111}} / (3\sqrt{3})$ | Rectangular | §7.1.2-11 |
| Convection | $u(m_{\text{conv}})$ | $= u(m_D)$ | Rectangular | §7.1.2-13 |
| Density of ref. mass | $u(m_c)$ | $m \cdot \rho_{\text{air}} \cdot u(\rho_{\text{ref}}) \,/\, \rho_{\text{ref}}^2$ | Rectangular | §7.1.2-7 |
| Calibration cert. unc. | $u(\text{cal\_ref})$ | $U_{\text{cert}} / k_{\text{cert}}$ | Normal | Replaces *u(mB)*, *u(mD)*, *u(mconv)* when cert. *U* is supplied |

> **Note on reference mass uncertainty:** When the test weight has a traceable calibration certificate with stated expanded uncertainty $U_{\text{cert}}$ and coverage factor $k_{\text{cert}}$, the combined reference mass uncertainty is taken directly as $u(\text{cal\_ref}) = U_{\text{cert}} / k_{\text{cert}}$, replacing the three MPE-based components.

#### Reference mass density uncertainty (EURAMET §7.1.2-7)

$$u(m_c) = \frac{m \cdot \rho_{\text{air}} \cdot u_{\text{std}}(\rho_{\text{ref}})}{\rho_{\text{ref}}^2}$$

where $u_{\text{std}}(\rho_{\text{ref}}) = u_{\text{half}} / \sqrt{3}$ (rectangular distribution) and class-based defaults are:

| OIML Class | $\rho_{\text{ref}}$ (kg/m³) | $u_{\text{half}}$ (kg/m³) |
|---|---|---|
| E1, E2, F1 | 7 950 | 140 |
| M1 | 8 400 | 170 |
| M2, M3 | 7 100 | 600 |

Air density defaults to $\rho_{\text{air}} = 1.2\ \text{kg/m}^3$ when barometric pressure is not recorded.

---

### 4.2 Combined Standard Uncertainty

$$u_c = \sqrt{u(\text{dig}_0)^2 + u(\text{dig}_L)^2 + u(\text{rep})^2 + u(\text{ecc})^2 + u(m_B)^2 + u(m_D)^2 + u(m_{\text{conv}})^2 + u(m_c)^2 + u(\text{cal\_ref})^2}$$

---

### 4.3 Effective Degrees of Freedom (Welch-Satterthwaite)

Of the nine components, only $u(\text{rep})$ is Type A with finite degrees of freedom ($\nu_{\text{rep}} = N - 1$, where *N* is the number of repeatability readings). All other components are Type B with $\nu \to \infty$. The Welch-Satterthwaite formula therefore reduces to:

$$\nu_{\text{eff}} = \frac{u_c^4 \cdot \nu_{\text{rep}}}{u(\text{rep})^4} = \frac{u_c^4 \cdot (N - 1)}{s_r^4}$$

When $u(\text{rep}) = 0$ (all repeatability readings identical), $\nu_{\text{eff}}$ is set to **510** by convention, giving $k \approx 2.000$.

---

### 4.4 Coverage Factor *k* (GUM Table G.2, *p* = 95.45 %)

| $\nu_{\text{eff}}$ | *k* |
|---|---|
| ≥ 30 | 2.000 |
| 20 – 29 | 2.086 |
| 15 – 19 | 2.131 |
| 10 – 14 | 2.228 |
| 9 | 2.262 |
| 8 | 2.306 |
| 7 | 2.365 |
| 6 | 2.447 |
| 5 | 2.571 |
| 4 | 2.776 |
| 3 | 3.182 |
| 2 | 4.303 |
| 1 | 12.706 |

For most calibration labs (N ≥ 10 repeatability readings), $\nu_{\text{eff}} \gg 30$ and $k = 2.000$.

---

### 4.5 Expanded Measurement Uncertainty

$$U = k \cdot u_c$$

The certificate states: *"The reported expanded uncertainty … is stated as the standard measurement uncertainty multiplied by the coverage factor k = [value], which for a t-distribution with $\nu_{\text{eff}}$ effective degrees of freedom corresponds to a coverage probability of approximately 95.45 %."*

---

## 5. Mass Standards Calibration (OIML R 111-1)

### 5.0 Internal Procedure Reference

Mass calibrations are performed under **QSL/QP/19 — Procedure for Calibration of Mass Standards**, which implements OIML R 111-1 using the ABBA substitution weighing method. Data sheets are recorded on form **QSL/QP/19/DS-MASS**.

Each calibration job records:
- Customer details and location
- Environmental conditions (start and end temperature and humidity, barometric pressure when available)
- Comparator details (model, serial number, capacity, scale division)
- One measurement block per weight piece, containing the ABBA balance readings and the reference standard metadata

---

### 5.1 ABBA Substitution Method

Masses are calibrated by comparison against a reference standard using a mass comparator. The weighing design is **ABBA**:

| Position | Reading |
|---|---|
| S₁ (standard, 1st placement) | S₁I₁, S₁I₂ |
| X₁ (unknown, 1st placement) | X₁I₁, X₁I₂ |
| X₂ (unknown, 2nd placement) | X₂I₁, X₂I₂ |
| S₂ (standard, 2nd placement) | S₂I₁, S₂I₂ |

#### Mean readings

$$\bar{S} = \frac{S_{1}I_1 + S_{1}I_2 + S_{2}I_1 + S_{2}I_2}{4}$$

$$\bar{X} = \frac{X_{1}I_1 + X_{1}I_2 + X_{2}I_1 + X_{2}I_2}{4}$$

#### Balance indication difference (grams)

$$\Delta = \bar{X} - \bar{S}$$

#### Reference standard correction

When a correction $C_{\text{ref}}$ (mg) is stated on the reference certificate:

$$\Delta_{\text{corrected}} = \Delta \times 1000 + C_{\text{ref}} \quad [\text{mg}]$$

#### Buoyancy correction (OIML R 111-1 Annex B)

Applied when the density of the item under test $\rho_{\text{item}}$ is known:

$$C_{\text{buoy}} = m \cdot \rho_{\text{air}} \left(\frac{1}{\rho_{\text{item}}} - \frac{1}{\rho_{\text{ref}}}\right) \times 10^6 \quad [\text{mg}]$$

where $m$ is the nominal mass in kg, $\rho_{\text{air}}$ is computed from environmental conditions, and $\rho_{\text{ref}} = 7\,950\ \text{kg/m}^3$ (stainless steel reference).

$$m_{\text{conv}} = \Delta_{\text{corrected}} + C_{\text{buoy}} \quad [\text{mg}]$$

#### Pass criterion

$$|m_{\text{conv}}| \leq \text{MPE}_{\text{R111}}(\text{nominal},\, \text{class})$$

MPE values are taken from OIML R 111-1 Table 1 (selection shown):

| Nominal | E1 (mg) | E2 (mg) | F1 (mg) | F2 (mg) | M1 (mg) | M2 (mg) | M3 (mg) |
|---|---|---|---|---|---|---|---|
| 5 000 kg | — | — | — | — | 25 000 | 80 000 | 250 000 |
| 2 000 kg | — | — | — | — | 10 000 | 30 000 | 100 000 |
| 1 000 kg | — | — | — | — | 5 000 | 16 000 | 50 000 |
| 500 kg | — | — | — | — | 2 500 | 8 000 | 25 000 |
| 200 kg | — | — | — | — | 1 000 | 3 000 | 10 000 |
| 100 kg | — | — | — | — | 500 | 1 600 | 5 000 |
| 50 kg | — | — | — | — | 250 | 800 | 2 500 |
| 20 kg | — | — | — | 800 | 100 | 300 | 1 000 |
| 10 kg | — | — | 50 | 160 | 50 | 160 | 500 |
| 5 kg | — | 25 | 25 | 80 | 25 | 80 | 250 |
| 2 kg | — | 10 | 10 | 30 | 10 | 30 | 100 |
| 1 kg | 0.5 | 5 | 5 | 16 | 5 | 16 | 50 |
| 500 g | 0.25 | 2.5 | 2.5 | 8 | 2.5 | 8 | 25 |
| 200 g | 0.10 | 1.0 | 1.0 | 3 | 1.0 | 3 | 10 |
| 100 g | 0.05 | 0.5 | 0.5 | 1.6 | 0.5 | 1.6 | 5 |
| 50 g | 0.030 | 0.25 | 0.25 | 0.8 | 0.25 | 0.8 | 2.5 |
| 20 g | 0.025 | 0.10 | 0.10 | 0.3 | 0.10 | 0.3 | 1.0 |
| 10 g | 0.020 | 0.05 | 0.05 | 0.16 | 0.05 | 0.16 | 0.5 |
| 5 g | 0.016 | 0.025 | 0.025 | 0.08 | 0.025 | 0.08 | 0.25 |
| 2 g | 0.012 | 0.012 | 0.012 | 0.04 | 0.012 | 0.04 | 0.12 |
| 1 g | 0.010 | 0.010 | 0.010 | 0.03 | 0.010 | 0.03 | 0.10 |
| 500 mg | 0.008 | 0.008 | 0.008 | 0.025 | 0.008 | 0.025 | — |
| 200 mg | 0.006 | 0.006 | 0.006 | 0.020 | 0.006 | 0.020 | — |
| 100 mg | 0.005 | 0.005 | 0.005 | 0.016 | 0.005 | 0.016 | — |
| 50 mg | — | — | — | 0.012 | — | 0.012 | — |
| 20 mg | — | — | — | 0.010 | — | 0.010 | — |
| 10 mg | — | — | — | 0.008 | — | 0.008 | — |
| 5 mg | — | — | — | 0.006 | — | — | — |
| 2 mg | — | — | — | 0.006 | — | — | — |
| 1 mg | — | — | — | 0.006 | — | — | — |

> Source: OIML R 111-1 (2004) Table 1. "—" denotes that the class is not defined at that nominal.

---

### 5.1a Conventional Mass and Certificate Result

The final result for each weight piece is expressed as the **conventional mass**, which is the nominal value plus (or minus) the measured error in milligrams:

$$m_{\text{conv}} = \text{nominal} \pm |m_{\text{final}}| \ \text{mg}$$

On the certificate this appears as, for example, **"10 kg + 500 mg"** or **"10 kg − 200 mg"**.

The reported quantity $m_{\text{final}}$ is:

$$m_{\text{final}} = \Delta_{\text{corrected}} + C_{\text{buoy}} \quad [\text{mg}]$$

where $\Delta_{\text{corrected}}$ is the ABBA difference after applying the reference standard's correction value, and $C_{\text{buoy}}$ is the buoyancy correction (zero when $\rho_{\text{item}}$ is unknown).

---

### 5.2 Air Density (BIPM Formula)

$$\rho_{\text{air}} = \frac{0.0034848 \times P}{273.15 + T} \left(1 - 0.378 \frac{e_w}{P}\right) \quad [\text{kg/m}^3]$$

where $P$ is barometric pressure in Pa, $T$ is temperature in °C, and $e_w$ is the partial pressure of water vapour (Pa) computed via the Magnus formula:

$$e_w = 610.78 \times 10^{\dfrac{7.5\,T}{237.3 + T}} \times \frac{H}{100}$$

with $H$ being relative humidity (%). Defaults to $\rho_{\text{air}} = 1.2\ \text{kg/m}^3$ when pressure is not recorded.

---

### 5.3 Mass Class Density Defaults (OIML R 111-1 Table B.7)

When the density of the reference standard is not stated on its certificate, the following class-based defaults are used:

| OIML Class | $\rho_{\text{ref}}$ (kg/m³) | Half-width $u_{\text{half}}$ (kg/m³) |
|---|---|---|
| E1, E2, F1, F2 | 7 950 | 140 |
| M1 | 8 400 | 170 |
| M2, M3 | 7 100 | 600 |

The standard uncertainty of the density is $u(\rho_{\text{ref}}) = u_{\text{half}} / \sqrt{3}$ (rectangular distribution).

---

### 5.4 Mass Uncertainty Budget (OIML R 111-1 Annex C.6)

Five uncertainty components, all expressed in **grams**. The certificate converts to milligrams (×1 000) for display.

| Component | Symbol | Formula | Distribution |
|---|---|---|---|
| Comparator repeatability | $u_{\text{bal}}$ | $s_r / \sqrt{N}$ where $s_r$ is sample SD of all mass readings | Normal (Type A) |
| Comparator resolution | $u_{\text{res}}$ | $d_{\text{comp}} / (2\sqrt{3})$ | Rectangular ±*d*/2 |
| Convection | $u_{\text{conv}}$ | $= u_{\text{res}}$ | Rectangular |
| Reference standard | $u_{\text{ref}}$ | $\text{MPE}_{\text{R111}} / (2\sqrt{3})$ or $U_{\text{cert}} / k_{\text{cert}}$ | Rectangular / Normal |
| Air buoyancy | $u_{\text{buoy}}$ | $m_{\text{kg}} \cdot \rho_{\text{air}} \cdot u(\rho_t) / \rho_t^2$ | Type B |

> **Note on $u_{\text{ref}}$:** When the reference standard's calibration certificate states an expanded uncertainty $U_{\text{cert}}$ (in grams) and coverage factor $k_{\text{cert}}$, the standard uncertainty is taken as $U_{\text{cert}} / k_{\text{cert}}$, replacing the MPE-based estimate. This is the preferred input when available.

> **Note on $u_{\text{buoy}}$ units:** The formula yields **grams**. No ×10³ or ×10⁶ factor is needed — $m_{\text{kg}}$ is in kg, $\rho_{\text{air}}$ and $\rho_t$ in kg/m³. The resulting $u_{\text{buoy}}$ in kg is numerically equal to grams after unit conversion within the comparator reading space.

$$u_c = \sqrt{u_{\text{bal}}^2 + u_{\text{res}}^2 + u_{\text{conv}}^2 + u_{\text{ref}}^2 + u_{\text{buoy}}^2} \quad [\text{g}]$$

Coverage factor $k$ is determined by Welch-Satterthwaite (same table as §4.4), though for mass comparator measurements the number of readings is typically large enough that $\nu_{\text{eff}} \geq 30$ and $k = 2.000$.

$$U = k \cdot u_c \quad [\text{g}] \quad \Rightarrow \quad U_{\text{mg}} = U \times 1\,000$$

#### Per-Block Uncertainty

The budget is evaluated **independently for each weight piece** (block) using that block's own mass readings and reference standard metadata. The global budget (across all blocks) uses a pooled SD over all mass readings and the most conservative reference standard parameters, and is used only as a fallback when per-block data is insufficient.

The certificate column **"Uncertainty in: ± mg"** reports the per-block $U_{\text{mg}}$.

---

## 6. Overall Pass / Fail Decision

A NAWI calibration passes overall only when all individual tests pass:

$$\text{Pass}_{\text{overall}} = \text{Pass}_{\text{ecc}} \wedge \text{Pass}_{\text{rep}} \wedge \text{Pass}_{\text{lin}} \wedge \text{Pass}_{\text{disc}}$$

Tests that are not performed (no data entered) are excluded from the conjunct — they do not cause a failure.

The minimum capacity adequacy check is **advisory only** and does not block certificate issuance.

---

## 7. Certificate Generation

### 7.1 NAWI Certificate Content

The NAWI calibration certificate includes:

- **Header** — laboratory name, certificate number, issue date, customer, instrument details
- **Environmental conditions** — temperature, humidity, and barometric pressure during calibration
- **Reference standards** — test weights used (class, serial number, traceability certificate number)
- **Test results summary** — eccentricity, repeatability, discrimination, linearity tables
- **Expanded measurement uncertainty** — $U$, coverage factor $k$, confidence level (~95.45 %)
- **Compliance statement** — whether the instrument meets the requirements of OIML R 76-1

### 7.2 Mass Certificate Content (KENAS format)

The mass calibration certificate follows the **KENAS / QSL standard layout** with two pages and seven numbered sections:

**Page 1**

| # | Section | Content |
|---|---|---|
| Header | — | Logo, "CALIBRATION CERTIFICATE", company address |
| Details | — | Requested by, address, equipment (MASS), type/model (CLASS Mx), serial number(s), location, calibration date, certificate no. |
| 1 | Reference Standards and Equipment Used | OIML R 111-1 statement; reference standard class, serial number, traceability certificate; comparator model/serial/capacity/division |
| 2 | Metrological Traceability | Traceability statement to KEBS national standards and SI |
| 3 | Calibration Procedure | "QSL/QP/19: Procedure for Calibration of [Class] Masses" |
| 4 | Environmental Conditions | Temperature range (°C), relative humidity range (%) |
| 5 | Validity | Expiry date (one year minus one day from issue date) |
| Sign-off | — | Calibrated by / Checked by / Approved by — each with name, date, signature line |

**Page 2**

| # | Section | Content |
|---|---|---|
| 6 | Measurement Results | Table: Nominal Mass \| Marking (serial no.) \| Conventional Mass \| Error Limit in ±mg \| Uncertainty in: ±mg |
| 7 | Comments | Results relate only to calibrated masses; pass/fail statement; uncertainty coverage factor statement (k, ~95%) |
| Footer | — | "--------End--------", page number |

#### Measurement Results Table Columns

| Column | Source | Notes |
|---|---|---|
| Nominal Mass | `block.nominalValue` | As entered on data sheet, e.g. "100 g" |
| Marking | `block.serialNo` | The weight's own serial/marking |
| Conventional Mass | $\text{nominal} \pm m_{\text{final}}\ \text{mg}$ | Formatted as "100 g + 0.77 mg" |
| Error Limit in ±mg | `block.mpeMilligrams` | OIML R 111-1 Table 1 MPE for class and nominal |
| Uncertainty in: ±mg | $U_{\text{mg}}$ per block | Per-block expanded uncertainty, $k = 2$ |

### 7.3 Certificate Numbering

Certificate numbers are assigned sequentially at the moment of generation. The number is stored on the Lab Work Order and is immutable once issued.

### 7.4 Formats

The system produces two outputs:

| Format | Route | Engine |
|---|---|---|
| Browser view (screen) | `GET /lab-work-order/certificate-data` | React (CalibrationCertificatePage) |
| PDF download | `GET /lab-work-order/certificate-pdf` | QuestPDF (.NET) |

The browser view reads the stored `calculatedResultsJson` and `rawDataJson`. The **Recalculate** function (`POST /lab-work-order/data-sheet/recalculate`) re-runs the full calculation pipeline against the stored raw data, updating `calculatedResultsJson` without changing any raw inputs. This is useful after a system update that improves the calculation logic.

---

## 8. Notation Summary

| Symbol | Meaning |
|---|---|
| *e* | Verification scale interval |
| *d* | Actual display readability (= *e* when not stated separately) |
| *L* | Test load |
| MPE(*L*) | Maximum permissible error at load *L* |
| $s_r$ | Sample standard deviation of repeatability readings |
| $\delta_{\max}$ | Maximum eccentricity deviation |
| $L_{\text{ecc}}$ | Eccentricity test load |
| $u_c$ | Combined standard uncertainty |
| $U$ | Expanded measurement uncertainty |
| *k* | Coverage factor |
| $\nu_{\text{eff}}$ | Effective degrees of freedom (Welch-Satterthwaite) |
| $\rho_{\text{air}}$ | Air density (kg/m³) |
| $\rho_{\text{ref}}$ | Density of reference mass (kg/m³) |
| $U_{\text{cert}}$ | Expanded uncertainty from reference weight's calibration certificate |
| $k_{\text{cert}}$ | Coverage factor stated on reference weight's certificate |
| $C_{\text{ref}}$ | Correction value (mg) from reference standard's certificate |
| $C_{\text{buoy}}$ | Air buoyancy correction (mg) |
| $m_{\text{final}}$ | Final mass correction: $\Delta_{\text{corrected}} + C_{\text{buoy}}$ (mg) |
| $m_{\text{conv}}$ | Conventional mass: nominal expressed with signed correction in mg |
| $\rho_{\text{item}}$ | Density of the mass under calibration (kg/m³) |
| $u_{\text{half}}$ | Half-width of rectangular density distribution (kg/m³) |

---

## 9. References

1. OIML R 76-1 (2006) — *Non-automatic weighing instruments. Part 1: Metrological and technical requirements*
2. OIML R 111-1 (2004) — *Weights of classes E1 to M3. Part 1: Metrological and technical requirements*
3. EURAMET cg-18 v4.0 (2015) — *Guidelines on the Determination of Uncertainty in Gravimetric Volume Calibration* [adapted for NAWI uncertainty budgets per §7.1.3-1a]
4. JCGM 100:2008 (GUM) — *Evaluation of measurement data — Guide to the Expression of Uncertainty in Measurement*
5. BIPM (1981) — *Formula for the determination of the density of moist air*
