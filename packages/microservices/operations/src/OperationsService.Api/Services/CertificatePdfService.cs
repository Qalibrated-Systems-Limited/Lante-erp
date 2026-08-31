using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;
using OperationsService.Core.DTOs.LabWorkOrders;

namespace OperationsService.Api.Services;

/// <summary>
/// Generates calibration certificate PDFs from CalibrationCertificateDto.
/// Supports NAWI (Balance / Weighbridge) and Mass Standard certificates.
///
/// Layout follows the ISO/IEC 17025:2015 house style: a double-ruled frame, a numbered
/// clause structure (identification → traceability → environment → results → statements →
/// authorisation), and a navy/gold palette. Both sheet types share the same chrome so a
/// client receiving one of each sees a single document family.
/// </summary>
public class CertificatePdfService
{
    // ── Theme ────────────────────────────────────────────────────────────────
    private const string Navy      = "#0E2340";   // section bars, table headers, headings
    private const string NavySoft  = "#1B3A63";   // secondary navy, sub-rules
    private const string Gold      = "#B8901F";   // frame, section underlines, accents
    private const string GoldLight = "#E0C572";   // table gridlines on navy
    private const string Cream     = "#FBF6E8";   // zebra / emphasis fills
    private const string Ink       = "#1A1A1A";   // body copy
    private const string Muted     = "#5A6675";   // captions, footnotes
    private const string Hair      = "#C9D2DE";   // light gridlines
    private const string Paper     = "#FFFFFF";

    // ── Issuing laboratory (default identity — used until a tenant sets their own
    // company.legal_name in system-settings; see TenantBranding below) ──────────
    private const string LabName     = "QALIBRATED SYSTEMS";
    private const string LabSuffix   = "LIMITED";
    private const string LabLine1    = "Calibration Laboratory · P.O. Box 47400–00100, Nairobi, Kenya";
    private const string LabLine2    = "www.qalibrated.co.ke · info@qalibrated.co.ke · +254 714 999 996";
    private const string Accredited  = "Issued under ISO/IEC 17025:2015 · Accredited by KENAS";

    /// <summary>
    /// A tenant's own company identity, fetched from user-service's system-settings via
    /// ITenantBrandingClient. Null means "no custom branding configured" — callers keep the
    /// default Qalibrated identity above untouched, so existing certificates render exactly as
    /// before unless a tenant has actually opted in by filling out Company Settings.
    /// </summary>
    public sealed record TenantBranding(string LegalName, string AddressLine, string ContactLine, string? DocCodePrefix = null);

    // Flows through the synchronous QuestPDF build graph kicked off by Generate() below without
    // threading a parameter through every private layout method; each Generate() call (even
    // concurrent ones, since this class is a DI singleton) gets its own isolated value.
    private static readonly AsyncLocal<TenantBranding?> CurrentBranding = new();

    // Position labels per eccentricity test type, used in the result table
    private static readonly string[] RadialPositions      = ["1 — Centre", "2 — Front left", "3 — Back left", "4 — Back right", "5 — Front right"];
    private static readonly string[] EndToEndPositions    = ["1 — First end", "2 — Second end"];
    private static readonly string[] EndMiddleEndPositions = ["1 — Front end", "2 — Middle", "3 — Back end"];

    // ── Watermark ────────────────────────────────────────────────────────────

    /// <summary>
    /// The house mark, pre-faded and flattened onto white. QuestPDF 2026.5.0 exposes no opacity
    /// control, so the fade is baked into the asset rather than applied at render time. Loaded once:
    /// re-reading it per certificate would re-parse the PNG on every download.
    /// </summary>
    private static readonly Lazy<byte[]?> Watermark = new(() =>
    {
        var assembly = typeof(CertificatePdfService).Assembly;
        using var stream = assembly.GetManifestResourceStream("OperationsService.Api.Assets.qc-watermark.png");
        if (stream is null) return null;   // a missing mark must not stop a certificate being issued
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    });

    /// <summary>
    /// Draws the content over a centred watermark. The mark sits on its own layer beneath the
    /// primary one, so it never participates in layout — the clause text lands in exactly the same
    /// place with or without it.
    /// </summary>
    private static void WithWatermark(IContainer container, Action<IContainer> content)
    {
        var mark = Watermark.Value;
        if (mark is null) { content(container); return; }

        container.Layers(layers =>
        {
            layers.Layer().AlignCenter().AlignMiddle().Width(340).Image(mark).FitArea();
            layers.PrimaryLayer().Element(content);
        });
    }

    // Unified entry point — picks NAWI or Mass generator based on SheetType
    public byte[] Generate(CalibrationCertificateDto cert, TenantBranding? branding = null)
    {
        CurrentBranding.Value = branding;
        try
        {
            return cert.SheetType == "Mass" ? GenerateMass(cert) : GenerateNawi(cert);
        }
        finally
        {
            CurrentBranding.Value = null;
        }
    }

    // ── NAWI ─────────────────────────────────────────────────────────────────

    private static byte[] GenerateNawi(CalibrationCertificateDto cert)
    {
        var n = cert.Nawi ?? new NawiCertificateSectionDto();
        string fallbackEquip = cert.SheetType == "NawiWeighbridge" ? "Weighbridge" : "Electronic balance";
        string equipLabel    = !string.IsNullOrWhiteSpace(n.EquipmentType) ? n.EquipmentType : fallbackEquip;

        // Density comment text derived from test-weight class
        string twClass = OperationsService.Core.Services.DataSheetCalculationService.NormaliseClass(n.TestWeightClass);
        (double rhoNom, double rhoU) = OperationsService.Core.Services.DataSheetCalculationService.GetClassDensity(twClass);

        string docId  = DocumentControlId(cert, "NAWI");
        string scope  = "Scope: Non-Automatic Weighing Instruments (NAWI)";
        string spine  = "NON-AUTOMATIC WEIGHING INSTRUMENT  ·  EURAMET cg-18";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page, docId, spine);

                page.Content().Element(c => Frame(c)).Element(f => WithWatermark(f, wm => wm.Column(col =>
                {
                    HeaderBand(col, cert, scope, docId, equipLabel, n.SerialNo);
                    IdentityStrip(col, cert, docId);

                    // ── 1. Client and item identification ─────────────────
                    SectionTitle(col, "1", "CLIENT AND ITEM IDENTIFICATION");
                    col.Item().PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            GroupLabel(left, "Requested by");
                            KeyVal(left, "Client:",              cert.CustomerName);
                            KeyVal(left, "Address:",             cert.CustomerAddress);
                            KeyVal(left, "Place of calibration:", cert.Location);
                            KeyVal(left, "Lab No.:",             cert.LabNo);
                            KeyVal(left, "Sticker No.:",         cert.StickerNumber);
                        });
                        row.ConstantItem(14);
                        row.RelativeItem().Column(right =>
                        {
                            GroupLabel(right, "Item calibrated");
                            KeyVal(right, "Equipment:",            equipLabel);
                            KeyVal(right, "Manufacturer / Model:", Join(" · ", n.Manufacturer, n.Model));
                            KeyVal(right, "Serial No.:",           n.SerialNo);
                            KeyVal(right, "Max capacity / interval d:", Join(" · ", n.MaximumCapacity, n.Division));
                            if (!string.IsNullOrWhiteSpace(n.RangeType))
                                KeyVal(right, "Range type:",       n.RangeType);
                            if (!string.IsNullOrWhiteSpace(n.AccuracyClass))
                                KeyVal(right, "Accuracy class:",   n.AccuracyClass);
                        });
                    });

                    // ── 2. Reference standards, method, traceability ──────
                    SectionTitle(col, "2", "REFERENCE STANDARDS, METHOD AND METROLOGICAL TRACEABILITY");
                    Clause(col, "2.1", "The weighing instrument was calibrated in accordance with EURAMET Calibration Guide No. 18, " +
                                       "Version 4.0 (11/2015) — Guidelines on the Calibration of Non-Automatic Weighing Instruments.");
                    Clause(col, "2.2", "Calibration procedure: repeatability, eccentricity and linearity (weighing) tests were performed " +
                                       "in accordance with EURAMET cg-18 v4.0 §4 (Determination of indication errors).");
                    Clause(col, "2.3", $"Reference standards used: standard masses of Class {Dash(n.TestWeightClass)}, " +
                                       $"serial No. {Dash(n.TestWeightSerialNo)}.");
                    Clause(col, "2.4", string.IsNullOrWhiteSpace(n.TestWeightCertificateNo)
                        ? "Traceability of reference standards: the standards are traceable to the national measurement standards."
                        : $"Traceability of reference standards: certificate No. {n.TestWeightCertificateNo}, issued by the Kenya Bureau of Standards (KEBS).");
                    Clause(col, "2.5", "This certificate documents traceability to the national measurement standards and to the units of " +
                                       "measurement realised at KEBS, or at other recognised national metrology institutes, in accordance " +
                                       "with the International System of Units (SI).");
                    Clause(col, "2.6", "Measurement uncertainty evaluated in accordance with JCGM 100:2008 (GUM) and EA-4/02 M:2022.");

                    // ── 3. Environmental conditions ───────────────────────
                    SectionTitle(col, "3", "ENVIRONMENTAL CONDITIONS DURING CALIBRATION");
                    EnvironmentBand(col, n.EnvironmentalConditions,
                        ("Disturbing influences:", "Vibration, air draughts and magnetic influence negligible"));

                    // ── 4. Measurement results ────────────────────────────
                    SectionTitle(col, "4", "MEASUREMENT RESULTS");
                    NawiLinearitySection(col, n.Linearity);
                    col.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Element(c => NawiEccentricitySection(c, n.Eccentricity));
                        row.ConstantItem(10);
                        row.RelativeItem().Element(c => NawiRepeatabilitySection(c, n.Repeatability));
                    });
                    NawiDiscriminationSection(col, n.Discrimination);

                    // ── 5. Statements and comments ────────────────────────
                    SectionTitle(col, "5", "STATEMENTS AND COMMENTS");
                    Clause(col, "5.1", "The results reported in clause 4 relate only to the instrument identified in clause 1 of this " +
                                       "certificate, at the location and under the conditions stated.");
                    Clause(col, "5.2", n.Tolerance?.OverallPass == false
                        ? "Some of the calculated errors of indication are outside the allowable tolerance for the instrument. Conformity is " +
                          "stated using simple acceptance (guard band w = 0), ISO/IEC 17025:2015 cl. 7.8.6."
                        : "The calculated errors of indication are within the allowable tolerance for the instrument. Conformity is stated " +
                          "using simple acceptance (guard band w = 0), ISO/IEC 17025:2015 cl. 7.8.6.");
                    Clause(col, "5.3", $"The mass standards used have assumed densities of {Num(rhoNom, 0)} kg/m³, with accompanying density " +
                                       $"uncertainties of {Num(rhoU, 0)} kg/m³.");
                    Clause(col, "5.4", "The reported expanded uncertainty of measurement is stated as the standard uncertainty of measurement " +
                                       "multiplied by the coverage factor k = 2, which, unless otherwise stated, corresponds to a coverage " +
                                       "probability of approximately 95 %.");
                    Clause(col, "5.5", "This certificate shall not be reproduced except in full, without the written approval of the issuing " +
                                       "laboratory. It does not of itself imply any product certification or approval by KENAS.");
                    Clause(col, "5.6", $"Validity: recalibration is recommended by {NextDue(cert)}. The recalibration interval is the " +
                                       "responsibility of the user.");
                    if (!string.IsNullOrWhiteSpace(cert.Notes))
                        Clause(col, "5.7", cert.Notes!);

                    // ── 6. Authorisation ──────────────────────────────────
                    SectionTitle(col, "6", "AUTHORISATION");
                    Authorisation(col, cert);
                })));
            });
        }).GeneratePdf();
    }

    private static void NawiLinearitySection(ColumnDescriptor col, List<NawiLinearityResultRowDto> rows)
    {
        if (rows.Count == 0) return;

        SubTitle(col, "4.1", "Weighing (linearity) test — indications before and after adjustment");
        col.Item().PaddingTop(3).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.1f);   // Nominal mass
                c.RelativeColumn(1.4f);   // Before adjustment
                c.RelativeColumn(1.4f);   // After adjustment
                c.RelativeColumn(1.2f);   // Error
                c.RelativeColumn(0.9f);   // Coverage factor
                c.RelativeColumn(1.6f);   // Expanded uncertainty
            });

            Th(t, "Nominal mass\n(g)");
            Th(t, "Indication before\nadjustment (g)");
            Th(t, "Indication after\nadjustment (g)");
            Th(t, "Error of\nindication (g)");
            Th(t, "Coverage\nfactor k");
            Th(t, "Expanded uncertainty\nU of indication (g)");

            bool zebra = false;
            foreach (var row in rows)
            {
                Td(t, Num(row.TestLoad, 0), zebra);
                Td(t, NumN(row.AsFoundIndication, 1), zebra);
                Td(t, NumN(row.DefinitiveIndication, 1), zebra);
                Td(t, SignedN(row.DefinitiveError, 1), zebra);
                Td(t, row.CoverageFactor > 0 ? Num(row.CoverageFactor, 1) : "", zebra);
                Td(t, Sig(row.UExpanded), zebra);
                zebra = !zebra;
            }
        });
    }

    private static IContainer NawiEccentricitySection(IContainer container, NawiEccentricityResultsDto? ecc)
    {
        if (ecc is null) return container;

        container.Column(col =>
        {
            SubTitle(col, "4.2", $"Eccentricity test — test load {G(ecc.TestLoad)} g");
            col.Item().PaddingTop(3).Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(1.7f); c.RelativeColumn(1.3f); c.RelativeColumn(1.5f); });
                Th(t, "Load position");
                Th(t, "Indication (g)");
                Th(t, "Deviation from centre (g)");

                var labels = GetEccLabels(ecc.Errors);
                bool zebra = false;
                for (int i = 0; i < ecc.Errors.Count; i++)
                {
                    bool    isRef = ecc.Errors[i] is null;
                    double? ind   = i < ecc.Indications.Count ? ecc.Indications[i] : null;
                    Td(t, i < labels.Length ? labels[i] : $"{i + 1}", zebra, left: true);
                    Td(t, ind.HasValue ? Num(ind.Value, 1) : "—", zebra);
                    Td(t, isRef ? "reference" : (ecc.Errors[i].HasValue ? Signed(ecc.Errors[i]!.Value, 1) : "—"), zebra);
                    zebra = !zebra;
                }

                Td(t, "Maximum deviation", zebra, left: true, bold: true);
                Td(t, "—", zebra);
                Td(t, Num(ecc.MaximumDeviation, 1), zebra, bold: true);
            });
            if (ecc.Mpe.HasValue)
                col.Item().PaddingTop(2).Text($"MPE ± {G(ecc.Mpe)} g · {PassLabel(ecc.Pass)}")
                   .FontSize(6.8f).FontColor(Muted).Italic();
        });
        return container;
    }

    private static IContainer NawiRepeatabilitySection(IContainer container, NawiRepeatabilityResultsDto? rep)
    {
        if (rep is null) return container;

        container.Column(col =>
        {
            SubTitle(col, "4.3", $"Repeatability test — test load {G(rep.TestLoad)} g");
            col.Item().PaddingTop(3).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1f); c.RelativeColumn(1.5f);
                    c.RelativeColumn(1f); c.RelativeColumn(1.5f);
                });
                Th(t, "Run"); Th(t, "Indication (g)");
                Th(t, "Run"); Th(t, "Indication (g)");

                // Errors are stored as (indication − test load); rebuild the indication for display.
                var indications = rep.Errors
                    .Select(e => e.HasValue ? rep.TestLoad + e.Value : (double?)null)
                    .ToList();

                int half = (indications.Count + 1) / 2;
                bool zebra = false;
                for (int i = 0; i < half; i++)
                {
                    Td(t, $"{i + 1}", zebra);
                    Td(t, indications[i].HasValue ? Num(indications[i]!.Value, 1) : "—", zebra);

                    int j = i + half;
                    Td(t, j < indications.Count ? $"{j + 1}" : "", zebra);
                    Td(t, j < indications.Count && indications[j].HasValue ? Num(indications[j]!.Value, 1) : "", zebra);
                    zebra = !zebra;
                }

                Td(t, "Standard deviation s", zebra, left: true, bold: true);
                Td(t, $"{Num(rep.StandardDeviation, 2)} g", zebra, bold: true);
                Td(t, "MPE", zebra, bold: true);
                Td(t, rep.Mpe.HasValue ? $"± {Num(rep.Mpe, 1)} g" : "—", zebra, bold: true);
            });
            col.Item().PaddingTop(2).Text(PassLabel(rep.Pass)).FontSize(6.8f).FontColor(Muted).Italic();
        });
        return container;
    }

    private static void NawiDiscriminationSection(ColumnDescriptor col, NawiDiscriminationResultDto? dis)
    {
        if (dis is null) return;

        SubTitle(col, "4.4", $"Discrimination test — test load {G(dis.TestLoad)} g");
        col.Item().PaddingTop(2).Text(
            $"Indication before {NumN(dis.Indication1, 1)} g · after {NumN(dis.Indication2, 1)} g · " +
            $"change {NumN(dis.IndicationChange, 1)} g (minimum required {NumN(dis.MinRequiredChange, 1)} g) · {PassLabel(dis.Pass)}")
           .FontSize(7.4f);
    }

    // ── MASS ─────────────────────────────────────────────────────────────────

    private static byte[] GenerateMass(CalibrationCertificateDto cert)
    {
        var m          = cert.Mass ?? new MassCertificateSectionDto();
        var firstBlock = m.BlockResults.FirstOrDefault();
        string massClass = firstBlock?.Class ?? "—";
        string serials   = Join(", ", m.BlockResults.Select(b => b.SerialNo).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray());

        string docId = DocumentControlId(cert, "MASS");
        string scope = "Scope: Mass — OIML Classes E₂, F₁, F₂, M₁, M₂, M₃";
        string spine = "MASS  CALIBRATION  ·  OIML R111-1:2004";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page, docId, spine);

                page.Content().Element(c => Frame(c)).Element(f => WithWatermark(f, wm => wm.Column(col =>
                {
                    HeaderBand(col, cert, scope, docId, "Mass (weight)", firstBlock?.SerialNo);
                    IdentityStrip(col, cert, docId);

                    // ── 1. Client and item identification ─────────────────
                    SectionTitle(col, "1", "CLIENT AND ITEM IDENTIFICATION");
                    col.Item().PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            GroupLabel(left, "Requested by");
                            KeyVal(left, "Client:",              cert.CustomerName);
                            KeyVal(left, "Address:",             cert.CustomerAddress);
                            KeyVal(left, "Place of calibration:", cert.Location);
                            KeyVal(left, "Lab No.:",             cert.LabNo);
                            KeyVal(left, "Sticker No.:",         cert.StickerNumber);
                        });
                        row.ConstantItem(14);
                        row.RelativeItem().Column(right =>
                        {
                            GroupLabel(right, "Item calibrated");
                            KeyVal(right, "Equipment:",     "Mass (weight)");
                            KeyVal(right, "Type / Class:",  massClass == "—" ? "—" : $"Class {massClass}");
                            KeyVal(right, "Nominal value:", firstBlock?.NominalValue);
                            KeyVal(right, "Serial / ID No.:", serials);
                            if (!string.IsNullOrWhiteSpace(m.ComparatorModel))
                                KeyVal(right, "Comparator:", Join(" · ", m.ComparatorModel, m.ComparatorSerialNo));
                        });
                    });

                    // ── 2. Reference standards, method, traceability ──────
                    SectionTitle(col, "2", "REFERENCE STANDARDS, METHOD AND METROLOGICAL TRACEABILITY");
                    Clause(col, "2.1", "The mass was calibrated in accordance with OIML R111-1:2004 — Weights of classes E₁, E₂, F₁, F₂, " +
                                       "M₁, M₁₋₂, M₂, M₂₋₃ and M₃, Part 1: Metrological and technical requirements.");
                    Clause(col, "2.2", $"Calibration procedure: procedure for the calibration of Class {massClass} masses " +
                                       "(direct comparison method).");
                    Clause(col, "2.3", $"Reference standard used: standard mass of Class {Dash(m.ReferenceStdClass)}, " +
                                       $"serial No. {Dash(m.ReferenceStdSerialNo)}.");
                    Clause(col, "2.4", string.IsNullOrWhiteSpace(m.ReferenceStdCertificateNo)
                        ? "Traceability of reference standard: the standard is traceable to the national measurement standards."
                        : $"Traceability of reference standard: certificate No. {m.ReferenceStdCertificateNo}, issued by the Kenya Bureau of Standards (KEBS).");
                    Clause(col, "2.5", "This certificate documents traceability to the national measurement standards and to the units of " +
                                       "measurement realised at KEBS, or at other recognised national metrology institutes, in accordance " +
                                       "with the International System of Units (SI).");
                    Clause(col, "2.6", "Measurement uncertainty evaluated in accordance with JCGM 100:2008 (GUM) and EA-4/02 M:2022.");

                    // ── 3. Environmental conditions ───────────────────────
                    SectionTitle(col, "3", "ENVIRONMENTAL CONDITIONS DURING CALIBRATION");
                    EnvironmentBand(col, m.EnvironmentalConditions,
                        ("Air density (assumed):", "1.2 kg/m³"));

                    // ── 4. Measurement results ────────────────────────────
                    SectionTitle(col, "4", "MEASUREMENT RESULTS");
                    col.Item().PaddingTop(3).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.2f);  // Nominal mass
                            c.RelativeColumn(1f);    // Marking
                            c.RelativeColumn(1.8f);  // Conventional mass
                            c.RelativeColumn(1.3f);  // Correction
                            c.RelativeColumn(1.5f);  // MPE
                            c.RelativeColumn(1.6f);  // Uncertainty
                        });

                        Th(t, "Nominal\nmass");
                        Th(t, "Marking");
                        Th(t, "Conventional mass\nvalue");
                        Th(t, "Correction\nfrom nominal");
                        Th(t, "Maximum permissible\nerror (± mg)");
                        Th(t, "Expanded uncertainty\nU (k = 2) (± mg)");

                        bool zebra = false;
                        foreach (var block in m.BlockResults)
                        {
                            double? corrMg = block.FinalCorrectionMg
                                ?? block.CorrectedDifferenceMg
                                ?? (block.MassDifference.HasValue ? block.MassDifference.Value * 1000.0 : null);

                            double? uMg = block.UExpanded.HasValue
                                ? Math.Round(block.UExpanded.Value * 1000.0, 1)
                                : null;

                            Td(t, block.NominalValue, zebra);
                            Td(t, string.IsNullOrWhiteSpace(block.SerialNo) ? "—" : block.SerialNo!, zebra);
                            Td(t, ConventionalMass(block.NominalValue, corrMg), zebra);
                            Td(t, corrMg.HasValue ? $"{Signed(corrMg.Value, 0)} mg" : "—", zebra);
                            Td(t, Num(block.MpeMilligrams, 0), zebra);
                            Td(t, Num(uMg, 0), zebra);
                            zebra = !zebra;
                        }
                    });
                    col.Item().PaddingTop(2).Text(
                        "Conventional mass value reported at a reference air density of 1.2 kg/m³ and a reference density of the weight of " +
                        "8 000 kg/m³ at 20 °C, per OIML D 28 and OIML R111-1:2004.")
                       .FontSize(6.6f).FontColor(Muted).Italic();

                    // ── 5. Statements and comments ────────────────────────
                    SectionTitle(col, "5", "STATEMENTS AND COMMENTS");
                    Clause(col, "5.1", "The results reported in clause 4 relate only to the mass identified in clause 1 of this certificate, " +
                                       "in the condition in which it was received.");
                    bool allPass = m.BlockResults.Count > 0 && m.BlockResults.All(b => b.WithinTolerance == true);
                    Clause(col, "5.2", allPass
                        ? $"The calibrated mass has errors within the maximum permissible error for Class {massClass} specified in " +
                          "OIML R111-1:2004. Conformity is stated using simple acceptance (guard band w = 0), ISO/IEC 17025:2015 cl. 7.8.6."
                        : $"Some calibrated masses have errors outside the maximum permissible error for Class {massClass} specified in " +
                          "OIML R111-1:2004. Conformity is stated using simple acceptance (guard band w = 0), ISO/IEC 17025:2015 cl. 7.8.6.");
                    Clause(col, "5.3", "The reported expanded uncertainty of measurement is stated as the standard uncertainty of measurement " +
                                       "multiplied by the coverage factor k = 2, which, unless otherwise stated, corresponds to a coverage " +
                                       "probability of approximately 95 %.");
                    Clause(col, "5.4", "This certificate shall not be reproduced except in full, without the written approval of the issuing " +
                                       "laboratory. It does not of itself imply any product certification or approval by KENAS.");
                    Clause(col, "5.5", $"Validity: recalibration is recommended by {NextDue(cert)}. The recalibration interval is the " +
                                       "responsibility of the user and depends on usage, handling and environment.");
                    if (!string.IsNullOrWhiteSpace(cert.Notes))
                        Clause(col, "5.6", cert.Notes!);

                    // ── 6. Uncertainty budget ─────────────────────────────
                    MassUncertaintyBudget(col, m);

                    // ── 7. Authorisation ──────────────────────────────────
                    SectionTitle(col, m.Uncertainty is null ? "6" : "7", "AUTHORISATION");
                    Authorisation(col, cert);
                })));
            });
        }).GeneratePdf();
    }

    private static void MassUncertaintyBudget(ColumnDescriptor col, MassCertificateSectionDto m)
    {
        var u = m.Uncertainty;
        if (u is null) return;

        SectionTitle(col, "6", "UNCERTAINTY OF MEASUREMENT — BUDGET SUMMARY");
        col.Item().PaddingTop(3).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.6f);  // Contribution
                c.RelativeColumn(1f);    // Symbol
                c.RelativeColumn(1.3f);  // Distribution
                c.RelativeColumn(1f);    // Divisor
                c.RelativeColumn(1.5f);  // Standard uncertainty
            });

            Th(t, "Contribution to uncertainty");
            Th(t, "Symbol");
            Th(t, "Distribution");
            Th(t, "Divisor");
            Th(t, "Standard uncertainty\nuᵢ (mg)");

            void Row(string label, string sym, string dist, string div, double? value, bool zebra)
            {
                Td(t, label, zebra, left: true);
                Td(t, sym, zebra);
                Td(t, dist, zebra);
                Td(t, div, zebra);
                Td(t, value.HasValue ? Mg(value.Value) : "—", zebra);
            }

            Row("Repeatability of the weighing process", "u(w)",  "normal",      "1",   u.UBalance,         false);
            Row("Resolution / readability of the comparator", "u(d)", "rectangular", "2√3", u.UResolution,   true);
            Row("Convection effects", "u(c)", "rectangular", "2√3", u.UConvection,                           false);
            Row("Uncertainty of the reference standard", "u(mᵣ)", "normal (k = 2)", "2", u.UReferenceWeight, true);
            Row($"Air buoyancy correction (ρ = {u.UAsssumedDensityKgM3:G} kg/m³)", "u(B)", "rectangular", "√3", u.UAirBuoyancy, false);

            Td(t, "Combined standard uncertainty  uᴄ = √Σuᵢ²", true, left: true, bold: true);
            Td(t, "uᴄ", true, bold: true);
            Td(t, "—", true);
            Td(t, "—", true);
            Td(t, u.UCombined.HasValue ? Mg(u.UCombined.Value) : "—", true, bold: true);

            Td(t, $"Expanded uncertainty  U = k · uᴄ  (k = {u.CoverageFactor:G}, ≈ {u.ConfidenceLevel})", false, left: true, bold: true);
            Td(t, "U", false, bold: true);
            Td(t, "normal", false);
            Td(t, "—", false);
            Td(t, u.UExpanded.HasValue ? Mg(u.UExpanded.Value) : "—", false, bold: true);
        });
        col.Item().PaddingTop(2).Text(
            "Budget prepared per JCGM 100:2008 (GUM) and EA-4/02 M:2022. Individual contributions are recorded on the laboratory " +
            "worksheet retained on file and available to the client on request.")
           .FontSize(6.6f).FontColor(Muted).Italic();
    }

    // ── Page chrome ──────────────────────────────────────────────────────────

    private static void ConfigurePage(PageDescriptor page, string docId, string spine)
    {
        page.Size(PageSizes.A4);
        page.MarginVertical(10);
        page.MarginHorizontal(20);
        page.DefaultTextStyle(x => x.FontSize(7.8f).FontColor(Ink).LineHeight(1.15f));
        page.PageColor(Paper);

        // Vertical spine label down the outer edge, mirroring the reference layout.
        page.Foreground().Row(row =>
        {
            row.ConstantItem(12).RotateLeft().AlignCenter().AlignMiddle()
               .Text(spine).FontSize(6.2f).FontColor("#8C99AB").Bold();
            row.RelativeItem();
        });

        page.Footer().PaddingTop(5).Column(f =>
        {
            f.Item().LineHorizontal(0.8f).LineColor(Gold);
            f.Item().PaddingTop(3).AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(6.4f).FontColor(Muted).Italic());
                t.Span("System-generated document · Document Control ID ");
                t.Span(docId).Bold().FontColor(NavySoft);
                t.Span(" · Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
                t.Span(" · — End of Certificate —");
            });
        });
    }

    // Double-ruled frame: gold outer keyline, thin navy inner keyline.
    private static IContainer Frame(IContainer container) =>
        container
            .Border(1.4f).BorderColor(Gold)
            .Padding(2.5f)
            .Border(0.5f).BorderColor(NavySoft)
            .Padding(6);

    private static void HeaderBand(ColumnDescriptor col, CalibrationCertificateDto cert,
                                   string scope, string docId, string equipment, string? serial)
    {
        var tb = CurrentBranding.Value;
        col.Item().Row(row =>
        {
            // Issuing laboratory
            row.RelativeItem(3.1f).Column(c =>
            {
                if (tb is null)
                {
                    c.Item().Text(t =>
                    {
                        t.Span(LabName).FontSize(12).Bold().FontColor(Navy);
                    });
                    c.Item().Text(LabSuffix).FontSize(9).Bold().FontColor(Gold);
                    c.Item().PaddingTop(2).Text(LabLine1).FontSize(6.3f).FontColor(Muted);
                    c.Item().Text(LabLine2).FontSize(6.3f).FontColor(Muted);
                }
                else
                {
                    c.Item().Text(t =>
                    {
                        t.Span(tb.LegalName).FontSize(12).Bold().FontColor(Navy);
                    });
                    if (!string.IsNullOrWhiteSpace(tb.AddressLine))
                        c.Item().PaddingTop(2).Text(tb.AddressLine).FontSize(6.3f).FontColor(Muted);
                    if (!string.IsNullOrWhiteSpace(tb.ContactLine))
                        c.Item().Text(tb.ContactLine).FontSize(6.3f).FontColor(Muted);
                }
            });

            // Title block
            row.RelativeItem(3.4f).AlignMiddle().Column(c =>
            {
                c.Item().AlignCenter().Text("CALIBRATION").FontSize(14).Bold().FontColor(Navy);
                c.Item().AlignCenter().Text("CERTIFICATE").FontSize(14).Bold().FontColor(Navy);
                c.Item().PaddingTop(2).AlignCenter().Text(Accredited).FontSize(6.3f).FontColor(Muted);
                c.Item().AlignCenter().Text(scope).FontSize(6.3f).Italic().FontColor(NavySoft);
            });

            // QR — encodes the certificate's own identifying details as plain text.
            row.RelativeItem(1.3f).AlignRight().Column(c =>
            {
                c.Item().AlignRight().Width(46).Height(46)
                 .Image(QrPng(QrPayload(cert, docId, equipment, serial)));
                c.Item().PaddingTop(1).AlignRight().Text("Scan for certificate details")
                 .FontSize(5.2f).FontColor(Muted);
            });
        });

        col.Item().PaddingTop(3).LineHorizontal(1.2f).LineColor(Gold);
    }

    // The four identifying fields, as a navy-labelled 2×2 strip.
    private static void IdentityStrip(ColumnDescriptor col, CalibrationCertificateDto cert, string docId)
    {
        col.Item().PaddingTop(4).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.35f); c.RelativeColumn(2f);
                c.RelativeColumn(1.35f); c.RelativeColumn(2f);
            });

            static void Label(TableDescriptor tbl, string text) =>
                tbl.Cell().Background(Navy).Border(0.4f).BorderColor(Gold)
                   .PaddingVertical(2.5f).PaddingHorizontal(5)
                   .Text(text).FontSize(6.8f).Bold().FontColor("#FFFFFF");

            static void Value(TableDescriptor tbl, string? text) =>
                tbl.Cell().Background(Cream).Border(0.4f).BorderColor(Gold)
                   .PaddingVertical(2.5f).PaddingHorizontal(5)
                   .Text(Dash(text)).FontSize(7.4f).Bold().FontColor(Navy);

            Label(t, "CERTIFICATE No.");      Value(t, cert.CertificateNumber);
            Label(t, "DOCUMENT CONTROL ID");  Value(t, docId);
            Label(t, "DATE OF CALIBRATION");  Value(t, cert.CertificateIssuedAt?.ToString("dd MMM yyyy"));
            Label(t, "NEXT CALIBRATION DUE"); Value(t, NextDue(cert));
        });
    }

    private static void Authorisation(ColumnDescriptor col, CalibrationCertificateDto cert)
    {
        string calDate = cert.CertificateIssuedAt?.ToString("dd MMM yyyy") ?? "—";
        string appDate = cert.TmApprovedAt?.ToString("dd MMM yyyy") ?? calDate;

        col.Item().PaddingTop(4).ShowEntire().Row(row =>
        {
            SignBlock(row, "CALIBRATED BY", cert.CalibrationDoneBy,
                      "Calibration Technician", calDate);
            row.ConstantItem(8);
            SignBlock(row, "APPROVED BY", cert.TmApprovedBy ?? cert.CheckedBy,
                      "Technical Manager · Authorised Signatory", appDate);
            row.ConstantItem(8);
            row.RelativeItem().Element(Boxed).Column(c =>
            {
                c.Item().AlignCenter().Text("OFFICIAL LABORATORY STAMP")
                 .FontSize(6.2f).Bold().FontColor(Muted);
                c.Item().Height(SignatureSpace + 4);
            });
        });
    }

    // Clear height of the blank space left for a wet signature. Both signature blocks read
    // from this one constant so their ruled lines always sit at the same height — two rules
    // at different heights on a signed document looks like one of them was edited.
    private const float SignatureSpace = 25f;

    private static void SignBlock(RowDescriptor row, string heading, string? name,
                                  string role, string date)
    {
        row.RelativeItem().Element(Boxed).Column(c =>
        {
            c.Item().Text(heading).FontSize(6.6f).Bold().FontColor(Navy);
            c.Item().Height(SignatureSpace);
            c.Item().LineHorizontal(0.5f).LineColor(NavySoft);
            c.Item().PaddingTop(2).Text(Dash(name)).FontSize(7.2f).Bold();
            c.Item().Text(role).FontSize(6.4f).FontColor(Muted);
            c.Item().Text($"Date: {date}").FontSize(6.4f).FontColor(Muted);
        });
    }

    private static IContainer Boxed(IContainer c) =>
        c.Border(0.6f).BorderColor(Hair).Padding(5);

    // ── Section furniture ────────────────────────────────────────────────────

    private static void SectionTitle(ColumnDescriptor col, string number, string title)
    {
        col.Item().PaddingTop(4).Row(row =>
        {
            row.ConstantItem(15).Text($"{number}.").FontSize(8f).Bold().FontColor(Gold);
            row.RelativeItem().Text(title).FontSize(8f).Bold().FontColor(Navy);
        });
        col.Item().PaddingTop(1.5f).LineHorizontal(0.9f).LineColor(Gold);
    }

    private static void SubTitle(ColumnDescriptor col, string number, string title)
    {
        col.Item().PaddingTop(4).Text(t =>
        {
            t.Span($"{number}  ").FontSize(7.4f).Bold().FontColor(Gold);
            t.Span(title).FontSize(7.4f).Bold().FontColor(NavySoft);
        });
    }

    private static void Clause(ColumnDescriptor col, string number, string body)
    {
        col.Item().PaddingTop(1.2f).Row(row =>
        {
            row.ConstantItem(20).PaddingLeft(4).Text(number).FontSize(7.2f).Bold().FontColor(NavySoft);
            row.RelativeItem().Text(body).FontSize(7.2f).Justify();
        });
    }

    private static void GroupLabel(ColumnDescriptor col, string text) =>
        col.Item().Text(text).FontSize(7.2f).Bold().FontColor(Gold);

    private static void KeyVal(ColumnDescriptor col, string label, string? value)
    {
        col.Item().PaddingTop(1f).Row(row =>
        {
            row.RelativeItem(2f).Text(label).FontSize(7f).Bold().FontColor(NavySoft);
            row.RelativeItem(3f).Text(Dash(value)).FontSize(7f);
        });
    }

    private static void EnvironmentBand(ColumnDescriptor col, EnvironmentalConditionsDto? env,
                                        (string Label, string Value) third)
    {
        col.Item().PaddingTop(3).Background(Cream).Border(0.5f).BorderColor(GoldLight).Padding(4).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Ambient temperature:").FontSize(7f).Bold().FontColor(NavySoft);
                c.Item().Text(Range(env?.StartTemperature, env?.EndTemperature, "°C")).FontSize(7.2f);
            });
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Relative humidity:").FontSize(7f).Bold().FontColor(NavySoft);
                c.Item().Text(Range(env?.StartHumidity, env?.EndHumidity, "% rh")).FontSize(7.2f);
            });
            row.RelativeItem().Column(c =>
            {
                c.Item().Text(third.Label).FontSize(7f).Bold().FontColor(NavySoft);
                c.Item().Text(third.Value).FontSize(7.2f);
            });
        });
    }

    // ── Table cells ──────────────────────────────────────────────────────────

    private static void Th(TableDescriptor t, string label) =>
        t.Cell().Background(Navy).Border(0.4f).BorderColor(GoldLight)
         .PaddingVertical(2.2f).PaddingHorizontal(2).AlignCenter()
         .Text(label).FontSize(6.6f).Bold().FontColor("#FFFFFF");

    private static void Td(TableDescriptor t, string value, bool zebra, bool left = false, bool bold = false)
    {
        var cell = t.Cell()
            .Background(zebra ? Cream : Paper)
            .Border(0.35f).BorderColor(Hair)
            .PaddingVertical(1.8f).PaddingHorizontal(3);

        var aligned = left ? cell : cell.AlignCenter();
        var text = aligned.Text(value).FontSize(7f);
        if (bold) text.Bold().FontColor(Navy);
    }

    // ── QR ───────────────────────────────────────────────────────────────────

    // Plain-text payload: the certificate identifies itself. No verification URL is
    // encoded — this deployment has no verification endpoint, and a QR that implied
    // one would be an authenticity claim the system cannot honour.
    private static string QrPayload(CalibrationCertificateDto cert, string docId, string equipment, string? serial)
    {
        var orgLine = CurrentBranding.Value?.LegalName ?? $"{LabName} {LabSuffix}";
        var lines = new List<string>
        {
            orgLine,
            $"Certificate No: {Dash(cert.CertificateNumber)}",
            $"Document Control ID: {docId}",
            $"Date of calibration: {cert.CertificateIssuedAt?.ToString("dd MMM yyyy") ?? "—"}",
            $"Next calibration due: {NextDue(cert)}",
            $"Item: {equipment}",
        };
        if (!string.IsNullOrWhiteSpace(serial)) lines.Add($"Serial No: {serial}");
        if (!string.IsNullOrWhiteSpace(cert.CustomerName)) lines.Add($"Client: {cert.CustomerName}");
        return string.Join('\n', lines);
    }

    private static byte[] QrPng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(10);
    }

    // ── Value helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Stable document control ID. Derived deterministically from the certificate number so
    /// that re-downloading a certificate always yields the same ID — a random or time-based
    /// suffix would make two copies of one certificate disagree about their own identity.
    /// </summary>
    private static string DocumentControlId(CalibrationCertificateDto cert, string kind)
    {
        string prefix = CurrentBranding.Value?.DocCodePrefix is { Length: > 0 } p ? p : "LT";
        string basis = cert.CertificateNumber ?? cert.JobNumber ?? cert.LabNo ?? prefix;
        uint hash = 2166136261u;                       // FNV-1a
        foreach (char ch in basis) { hash ^= ch; hash *= 16777619u; }
        string date = (cert.CertificateIssuedAt ?? DateTime.UtcNow).ToString("yyyyMMdd");
        return $"{prefix}-{kind}-{date}-{hash % 1000000u:D6}";
    }

    private static string NextDue(CalibrationCertificateDto cert) =>
        cert.CertificateIssuedAt?.AddYears(1).AddDays(-1).ToString("dd MMM yyyy") ?? "—";

    private static string Range(double? start, double? end, string unit)
    {
        if (start is null && end is null) return "—";
        if (start is null) return $"{end:F1} {unit}";
        if (end is null || Math.Abs(end.Value - start.Value) < 1e-9) return $"{start:F1} {unit}";
        return $"{start:F1} {unit} – {end:F1} {unit}";
    }

    private static string[] GetEccLabels(List<double?> errors) =>
        errors.Count == 5 ? RadialPositions :
        errors.Count == 3 ? EndMiddleEndPositions :
                            EndToEndPositions;

    private static string ConventionalMass(string nominal, double? corrMg)
    {
        if (!corrMg.HasValue || corrMg.Value == 0) return nominal;
        string sign = corrMg.Value > 0 ? "+" : "−";
        return $"{nominal} {sign} {Num(Math.Abs(corrMg.Value), 0)} mg";
    }

    private static string PassLabel(bool? pass) =>
        pass == true ? "PASS" : pass == false ? "FAIL" : "";

    private static string Join(string sep, params string?[] parts) =>
        string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string Dash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;

    // Metrology house style: digits grouped by a non-breaking space (30 000.2, not 30,000.2),
    // decimal point, and a fixed number of decimals per column so a result column reads as a
    // column rather than a ragged set of shortest-form numbers.
    private static readonly System.Globalization.NumberFormatInfo Nfi = new()
    {
        NumberGroupSeparator   = " ",
        NumberDecimalSeparator = ".",
    };

    private static string Num(double v, int dec)  => v.ToString($"N{dec}", Nfi);
    private static string Num(double? v, int dec) => v.HasValue ? Num(v.Value, dec) : "—";
    private static string NumN(double? v, int dec) => v.HasValue ? Num(v.Value, dec) : "";

    /// <summary>
    /// Signed deviation. A value that rounds to zero at the displayed precision is written "0.0"
    /// with no sign — "+ 0.0" would assert a positive error the measurement does not support.
    /// </summary>
    private static string Signed(double v, int dec)
    {
        double halfUlp = 0.5 * Math.Pow(10, -dec);
        if (Math.Abs(v) < halfUlp) return Num(0, dec);
        return v > 0 ? $"+ {Num(v, dec)}" : $"− {Num(Math.Abs(v), dec)}";
    }

    private static string SignedN(double? v, int dec) => v.HasValue ? Signed(v.Value, dec) : "";

    // Uncertainties span several decades (3.1E-4 … 0.73), so show 4 significant figures
    // rather than forcing a fixed decimal count that would print 0.00 for the small ones.
    private static string Sig(double? v)  => v.HasValue ? v.Value.ToString("G4", Nfi) : "";
    private static string G(double v)     => v.ToString("G", Nfi);
    private static string G(double? v)    => v.HasValue ? v.Value.ToString("G", Nfi) : "—";
    private static string Mg(double v)    => (v * 1000.0).ToString("G4", Nfi);
}
