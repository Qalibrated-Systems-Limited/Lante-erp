using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HrService.Core.DTOs.Payroll;

namespace HrService.Api.Services;

/// <summary>
/// H6 (P10, HR-009/HR-010) — payslip and P9 annual tax certificate PDFs. Follows the operations
/// CertificatePdfService pattern: server-side QuestPDF, generated on demand and streamed.
/// <para><b>Generated on request, never stored.</b> A payslip is rendered from the payslip lines, which are
/// frozen the moment a run is approved, so re-rendering next year produces exactly the same document. Storing
/// the bytes as well would add a second copy that can only ever drift from — or outlive — the data it came
/// from, and HR has no blob store to put it in.</para>
/// </summary>
public class PayrollPdfService
{
    private const string Ink = "#1B3A5C";      // the QSL navy the rest of the product uses
    private const string Muted = "#64748B";
    private const string Rule = "#E2E8F0";

    public byte[] GeneratePayslip(PayslipDto slip, string companyName) =>
        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Black));

                page.Header().Element(h => Header(h, companyName, "PAYSLIP", slip.PayrollPeriodCode));

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Element(e => EmployeeBlock(e, slip));

                    // Earnings and deductions side by side, each in payslip order — the order HR configured
                    // on the salary structure, not an order the renderer invented.
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().PaddingRight(6).Element(e =>
                            LineTable(e, "Earnings", slip.Lines.Where(l => l.LineType == "Earning").ToList(), slip.CurrencyCode, slip.TotalEarnings));
                        row.RelativeItem().PaddingLeft(6).Element(e =>
                            LineTable(e, "Deductions", slip.Lines.Where(l => l.LineType == "Deduction").ToList(), slip.CurrencyCode, slip.TotalDeductions));
                    });

                    col.Item().Element(e => NetPayBanner(e, slip));

                    var employerLines = slip.Lines.Where(l => l.LineType == "EmployerCost").ToList();
                    if (employerLines.Count > 0)
                        col.Item().Element(e => LineTable(e, "Employer contributions (not deducted from you)",
                            employerLines, slip.CurrencyCode, slip.EmployerCost));

                    col.Item().Element(e => TaxSummary(e, slip));
                });

                page.Footer().Element(f => Footer(f, "This payslip is computer generated. Queries go to HR."));
            });
        }).GeneratePdf();

    /// <summary>
    /// HR-010 — the P9 annual tax deduction card: one row per month of the year, from the payslips already
    /// produced. Nothing is recalculated; if a month is missing from the table, no payroll was run for it.
    /// </summary>
    public byte[] GenerateP9(P9CertificateDto cert, string companyName) =>
        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(t => t.FontSize(8).FontColor(Colors.Black));

                page.Header().Element(h => Header(h, companyName, "TAX DEDUCTION CARD (P9)", cert.Year.ToString()));

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(t => { t.Span("Employee: ").SemiBold(); t.Span($"{cert.EmployeeName} ({cert.EmployeeNumber})"); });
                            c.Item().Text(t => { t.Span("KRA PIN: ").SemiBold(); t.Span(cert.KraPin ?? "not on file"); });
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(t => { t.Span("Months paid: ").SemiBold(); t.Span(cert.Months.Count.ToString()); });
                            c.Item().Text(t => { t.Span("Employer: ").SemiBold(); t.Span(companyName); });
                        });
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.2f);
                            for (var i = 0; i < 6; i++) c.RelativeColumn(1.3f);
                        });

                        foreach (var h in new[] { "Month", "Gross pay", "Taxable pay", "Tax charged", "Personal relief", "PAYE deducted", "Statutory" })
                            table.Cell().Element(HeadCell).Text(h).SemiBold().FontColor(Colors.White);

                        foreach (var m in cert.Months)
                        {
                            table.Cell().Element(BodyCell).Text(m.PeriodCode);
                            table.Cell().Element(BodyCell).AlignRight().Text($"{m.GrossPay:N2}");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{m.TaxableIncome:N2}");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{m.TaxCharged:N2}");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{m.PersonalRelief:N2}");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{m.Paye:N2}");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{m.StatutoryDeductions:N2}");
                        }

                        table.Cell().Element(TotalCell).Text("TOTAL").SemiBold();
                        table.Cell().Element(TotalCell).AlignRight().Text($"{cert.TotalGross:N2}").SemiBold();
                        table.Cell().Element(TotalCell).AlignRight().Text($"{cert.TotalTaxable:N2}").SemiBold();
                        table.Cell().Element(TotalCell).AlignRight().Text($"{cert.TotalTaxCharged:N2}").SemiBold();
                        table.Cell().Element(TotalCell).AlignRight().Text($"{cert.TotalRelief:N2}").SemiBold();
                        table.Cell().Element(TotalCell).AlignRight().Text($"{cert.TotalPaye:N2}").SemiBold();
                        table.Cell().Element(TotalCell).AlignRight().Text($"{cert.TotalStatutory:N2}").SemiBold();
                    });

                    if (cert.Months.Count < 12)
                        col.Item().Text($"Covers {cert.Months.Count} month(s) — only periods with an approved payroll run appear.")
                            .FontSize(7).FontColor(Muted).Italic();
                });

                page.Footer().Element(f => Footer(f, "Generated from approved payroll runs. Verify against your own records before filing."));
            });
        }).GeneratePdf();

    // ── Shared furniture ──────────────────────────────────────────────────────
    private static void Header(IContainer c, string company, string title, string subtitle) =>
        c.BorderBottom(2).BorderColor(Ink).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(company).FontSize(15).SemiBold().FontColor(Ink);
                col.Item().Text(title).FontSize(9).FontColor(Muted).LetterSpacing(0.1f);
            });
            row.ConstantItem(150).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text(subtitle).FontSize(13).SemiBold().FontColor(Ink);
                col.Item().AlignRight().Text($"Issued {DateTime.UtcNow:dd MMM yyyy}").FontSize(7).FontColor(Muted);
            });
        });

    private static void EmployeeBlock(IContainer c, PayslipDto s) =>
        c.Background("#F8FAFC").Border(1).BorderColor(Rule).Padding(10).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(t => { t.Span("Employee: ").SemiBold(); t.Span(s.EmployeeName ?? ""); });
                col.Item().Text(t => { t.Span("Number: ").SemiBold(); t.Span(s.EmployeeNumber ?? ""); });
                col.Item().Text(t => { t.Span("Department: ").SemiBold(); t.Span(s.DepartmentName ?? "—"); });
            });
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(t => { t.Span("Period: ").SemiBold(); t.Span(s.PayrollPeriodCode); });
                col.Item().Text(t => { t.Span("Basic salary: ").SemiBold(); t.Span($"{s.CurrencyCode} {s.BasicSalary:N2}"); });
                col.Item().Text(t => { t.Span("KRA PIN: ").SemiBold(); t.Span(s.KraPin ?? "not on file"); });
            });
        });

    private static void LineTable(IContainer c, string heading, List<PayslipLineDto> lines, string ccy, decimal total) =>
        c.Column(col =>
        {
            col.Item().PaddingBottom(4).Text(heading).SemiBold().FontColor(Ink);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cd => { cd.RelativeColumn(2.4f); cd.RelativeColumn(1.2f); });

                if (lines.Count == 0)
                {
                    table.Cell().ColumnSpan(2).Element(BodyCell).Text("None").FontColor(Muted).Italic();
                }
                else
                {
                    foreach (var l in lines)
                    {
                        table.Cell().Element(BodyCell).Column(cell =>
                        {
                            cell.Item().Text(l.Name);
                            if (!string.IsNullOrWhiteSpace(l.Basis))
                                cell.Item().Text(l.Basis).FontSize(6.5f).FontColor(Muted);
                        });
                        table.Cell().Element(BodyCell).AlignRight().Text($"{l.Amount:N2}");
                    }
                }

                table.Cell().Element(TotalCell).Text($"Total {heading.ToLowerInvariant()}").SemiBold();
                table.Cell().Element(TotalCell).AlignRight().Text($"{ccy} {total:N2}").SemiBold();
            });
        });

    private static void NetPayBanner(IContainer c, PayslipDto s) =>
        c.Background(Ink).Padding(12).Row(row =>
        {
            row.RelativeItem().AlignMiddle().Text("NET PAY").FontSize(11).SemiBold().FontColor(Colors.White);
            row.ConstantItem(200).AlignRight().AlignMiddle()
                .Text($"{s.CurrencyCode} {s.NetPay:N2}").FontSize(16).SemiBold().FontColor(Colors.White);
        });

    private static void TaxSummary(IContainer c, PayslipDto s) =>
        c.Border(1).BorderColor(Rule).Padding(10).Column(col =>
        {
            col.Item().PaddingBottom(4).Text("How the tax was worked out").SemiBold().FontColor(Ink);
            col.Item().Row(row =>
            {
                void Cell(string label, string value) => row.RelativeItem().Column(cc =>
                {
                    cc.Item().Text(label).FontSize(6.5f).FontColor(Muted);
                    cc.Item().Text(value).FontSize(9).SemiBold();
                });
                Cell("Gross pay", $"{s.GrossPay:N2}");
                Cell("Taxable income", $"{s.TaxableIncome:N2}");
                Cell("PAYE", $"{s.Paye:N2}");
                Cell("Personal relief", $"{s.PersonalRelief:N2}");
                Cell("Statutory", $"{s.StatutoryDeductions:N2}");
                Cell("Other deductions", $"{s.OtherDeductions:N2}");
            });
            if (s.UnpaidDays > 0)
                col.Item().PaddingTop(6).Text($"Includes {s.UnpaidDays:0.##} unpaid day(s) — {s.CurrencyCode} {s.UnpaidDeduction:N2} not earned this period.")
                    .FontSize(7).FontColor(Muted);
            if (s.OvertimeHours > 0)
                col.Item().Text($"Includes {s.OvertimeHours:0.##} overtime hour(s) paying {s.CurrencyCode} {s.OvertimePay:N2}.")
                    .FontSize(7).FontColor(Muted);
        });

    private static void Footer(IContainer c, string note) =>
        c.BorderTop(1).BorderColor(Rule).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(note).FontSize(6.5f).FontColor(Muted);
            row.ConstantItem(80).AlignRight().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(6.5f).FontColor(Muted));
                t.CurrentPageNumber(); t.Span(" / "); t.TotalPages();
            });
        });

    private static IContainer HeadCell(IContainer c) => c.Background(Ink).PaddingVertical(4).PaddingHorizontal(5);
    private static IContainer BodyCell(IContainer c) => c.BorderBottom(1).BorderColor(Rule).PaddingVertical(3).PaddingHorizontal(5);
    private static IContainer TotalCell(IContainer c) => c.BorderTop(1).BorderColor(Ink).PaddingVertical(4).PaddingHorizontal(5);
}
