using HrService.Core.DTOs.Payroll;

namespace HrService.Core.Interfaces.Services;

/// <summary>
/// H6 pass 2 (P10 + P11, HR-009/010/011) — the paperwork a payroll run produces: bank payment files, the
/// second journal that moves net pay out of the holding account, and the P9 annual tax certificate.
/// <para>The payslip and P9 PDFs are rendered in the API layer from these DTOs, on demand — see
/// <c>PayrollPdfService</c> for why nothing is stored.</para>
/// </summary>
public interface IPayrollDocumentService
{
    // ── Bank payment files (P11) ──
    Task<List<BankPaymentFileDto>> ListBankFilesAsync(string? runId);

    /// <summary>
    /// Builds the bank upload for an APPROVED run. Staff with no primary bank account are named and an alert
    /// is raised rather than being quietly left out of the file (P11 design note) — a silently short payment
    /// file is how someone goes unpaid without anyone noticing.
    /// </summary>
    Task<PayrollActionResult> GenerateBankFileAsync(string runId, GenerateBankFileDto dto, string? tenantSchema, string userId);

    /// <summary>The stored CSV, exactly as generated. Stamps who took it.</summary>
    Task<(string FileName, string Content)?> DownloadBankFileAsync(string id, string userId);

    /// <summary>
    /// Records that the file really was uploaded to the bank, and posts the second journal:
    /// Dr the net-pay holding account, Cr the bank the money left (P11 step 11.5).
    /// </summary>
    Task<PayrollActionResult> ConfirmBankFileAsync(string id, ConfirmBankFileDto dto, string? tenantSchema, string userId, string? userName, CancellationToken ct = default);

    // ── Documents (P10, HR-010) ──
    Task<PayslipDto?> GetPayslipAsync(string id);
    /// <summary>The year's approved payslips for one employee, totalled. Nothing is recalculated.</summary>
    Task<P9CertificateDto?> GetP9Async(string employeeId, int year);
}
