using System.Text;
using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Payroll;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H6 pass 2 (P10 + P11) — bank payment files, the payment journal, and the P9 annual tax certificate.
/// <para><b>The generated CSV is stored, the PDFs are not.</b> A bank file is an artefact somebody approves and
/// then uploads: it must be byte-for-byte what was approved, so regenerating it later against changed bank
/// details would be wrong. A payslip PDF is the opposite — it is a rendering of frozen payslip lines, so
/// re-rendering always produces the same document and a stored copy would only be a second thing to keep in
/// step.</para>
/// <para><b>Nobody is silently left out.</b> An employee with no primary bank account cannot be in the file,
/// so they are named on it, counted, and an alert is raised. A short payment file that looks complete is how
/// someone goes unpaid for a month without anyone noticing.</para>
/// </summary>
public class PayrollDocumentService(
    IGenericRepository<PayrollRun> runs,
    IGenericRepository<Payslip> payslips,
    IGenericRepository<PayslipLine> payslipLines,
    IGenericRepository<EmployeeBankDetail> bankDetails,
    IGenericRepository<Employee> employees,
    IGenericRepository<BankPaymentFile> bankFiles,
    IGenericRepository<HrAuditLog> audit,
    IFinanceGateway finance,
    IHrAlertGateway notifier) : IPayrollDocumentService
{
    /// <summary>The holding account net pay sat in from run approval — this journal empties it.</summary>
    private const string GlNetPayHolding = "2110";

    /// <summary>
    /// The bank layouts. Each is an ASSUMPTION about the bank's bulk-salary template, not a transcription of
    /// one — which is why every generated file carries <c>NeedsFormatConfirmation</c> until a human has
    /// checked it against the real thing. Holding them as data makes correcting one a one-line change.
    /// </summary>
    private static readonly Dictionary<BankFormat, (string Bank, string[] Headers, Func<BankRow, string[]> Row)> Formats = new()
    {
        [BankFormat.Generic] = ("Generic", ["AccountNumber", "AccountName", "Amount", "Narration"],
            r => [r.AccountNumber, r.AccountName, r.Amount, r.Narration]),

        [BankFormat.Kcb] = ("KCB", ["AccountNumber", "AccountName", "Amount", "Currency", "Narration"],
            r => [r.AccountNumber, r.AccountName, r.Amount, r.Currency, r.Narration]),

        [BankFormat.Equity] = ("Equity (EazzyPay)", ["AccountNumber", "AccountName", "Amount", "Reference", "Narration"],
            r => [r.AccountNumber, r.AccountName, r.Amount, r.Reference, r.Narration]),

        [BankFormat.Ncba] = ("NCBA", ["AccountNumber", "AccountName", "Amount", "Currency", "Reference", "Narration"],
            r => [r.AccountNumber, r.AccountName, r.Amount, r.Currency, r.Reference, r.Narration]),

        [BankFormat.CoOp] = ("Co-operative Bank", ["AccountNumber", "AccountName", "Amount", "Narration", "Reference"],
            r => [r.AccountNumber, r.AccountName, r.Amount, r.Narration, r.Reference]),
    };

    private record BankRow(string AccountNumber, string AccountName, string Amount, string Currency, string Reference, string Narration);

    // ══════════════════════════════════════════════════════════════════════════════
    // Bank payment files (P11)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<BankPaymentFileDto>> ListBankFilesAsync(string? runId)
    {
        var q = bankFiles.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(runId)) q = q.Where(f => f.PayrollRunId == runId);
        var list = await q.OrderByDescending(f => f.GeneratedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<PayrollActionResult> GenerateBankFileAsync(string runId, GenerateBankFileDto dto, string? tenantSchema, string userId)
    {
        var run = await runs.GetByIdAsync(runId);
        if (run is null || run.IsDeleted) return Err("Payroll run not found.");
        // P11 step 11.1 — an unapproved run must never reach a bank.
        if (run.Status != PayrollRunStatus.Approved)
            return Err($"{run.RunNumber} is {run.Status.ToString().ToLowerInvariant()} — only an approved run can be paid.");

        if (!Enum.TryParse<BankFormat>(dto.Format, true, out var format))
            return Err($"The format must be one of {string.Join(", ", Enum.GetNames<BankFormat>())}.");

        var slips = await payslips.Query().AsNoTracking()
            .Where(p => p.PayrollRunId == run.Id).OrderBy(p => p.EmployeeNumber).ToListAsync();
        if (slips.Count == 0) return Err("The run has no payslips.");

        var employeeIds = slips.Select(s => s.EmployeeId).ToList();
        var accounts = await bankDetails.Query().AsNoTracking()
            .Where(b => employeeIds.Contains(b.EmployeeId) && b.IsPrimary && b.IsActive).ToListAsync();

        var (bank, headers, project) = Formats[format];
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", headers));

        var missing = new List<string>();
        decimal total = 0m;
        var included = 0;

        foreach (var slip in slips)
        {
            var account = accounts.FirstOrDefault(a => a.EmployeeId == slip.EmployeeId);
            if (account is null || string.IsNullOrWhiteSpace(account.AccountNumber))
            {
                missing.Add($"{slip.EmployeeNumber} {slip.EmployeeName}");
                continue;
            }
            // Nobody is paid a zero or negative amount through a bulk file; that needs looking at, not sending.
            if (slip.NetPay <= 0m)
            {
                missing.Add($"{slip.EmployeeNumber} {slip.EmployeeName} (net pay is {slip.NetPay:N2})");
                continue;
            }

            var row = new BankRow(
                account.AccountNumber.Trim(),
                (account.AccountName ?? slip.EmployeeName ?? "").Trim(),
                slip.NetPay.ToString("F2"),
                slip.CurrencyCode,
                $"{run.RunNumber}-{slip.EmployeeNumber}",
                $"Salary {run.PayrollPeriodCode}");

            csv.AppendLine(string.Join(",", project(row).Select(Csv)));
            total += slip.NetPay;
            included++;
        }

        if (included == 0)
            return Err("No employee on this run has a primary bank account — nothing could be written to the file.");

        var fileName = $"{run.RunNumber}-{format.ToString().ToLowerInvariant()}.csv";
        var created = await bankFiles.CreateAsync(new BankPaymentFile
        {
            PayrollRunId = run.Id,
            PayrollPeriodCode = run.PayrollPeriodCode,
            Format = format,
            BankName = bank,
            FileName = fileName,
            Content = csv.ToString(),
            EmployeeCount = included,
            TotalAmount = Round(total),
            MissingBankDetails = missing.Count == 0 ? null : string.Join(" | ", missing),
            MissingCount = missing.Count,
            GeneratedBy = userId,
            GeneratedAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });

        var result = new PayrollActionResult("Created",
            $"{fileName}: {included} payment(s) totalling {run.CurrencyCode} {total:N2} in the {bank} format.", created.Id);
        result.Warnings.Add($"The {bank} column layout has NOT been checked against the bank's own template — verify it before a live upload.");

        if (missing.Count > 0)
        {
            result.Warnings.Add($"{missing.Count} employee(s) are NOT in this file and will not be paid by it: {string.Join("; ", missing)}");
            await NotifyAsync(tenantSchema, "Critical",
                $"{missing.Count} employee(s) missing from the {run.PayrollPeriodCode} bank file",
                $"{run.RunNumber} generated a {bank} payment file covering {included} of {slips.Count} employees. Not included: {string.Join("; ", missing)}. Add a primary bank account and regenerate the file.",
                "hr.payroll.write");
        }

        if (total != run.TotalNet)
            result.Warnings.Add($"The file pays {total:N2} against the run's net total of {run.TotalNet:N2} — the difference is the excluded staff.");

        await LogAsync("BankPaymentFile", created.Id, HrAuditAction.BankFileGenerated,
            $"{fileName} generated for {run.RunNumber}: {included} payment(s), {total:N2}, {missing.Count} excluded.", userId);
        return result;
    }

    public async Task<(string FileName, string Content)?> DownloadBankFileAsync(string id, string userId)
    {
        var file = await bankFiles.GetByIdAsync(id);
        if (file is null || file.IsDeleted) return null;

        file.DownloadedBy = userId;
        file.DownloadedAt = DateTime.UtcNow;
        Touch(file, userId);
        await bankFiles.UpdateAsync(file);
        return (file.FileName, file.Content);
    }

    public async Task<PayrollActionResult> ConfirmBankFileAsync(string id, ConfirmBankFileDto dto, string? tenantSchema,
        string userId, string? userName, CancellationToken ct = default)
    {
        var file = await bankFiles.GetByIdAsync(id);
        if (file is null || file.IsDeleted) return Err("Bank payment file not found.");
        if (file.ConfirmedAt is not null && file.JournalEntryId is not null)
            return new PayrollActionResult("NoChange", $"{file.FileName} was already confirmed and posted as {file.JournalEntryNo}.", file.Id);
        if (string.IsNullOrWhiteSpace(dto.BankGlAccountId))
            return Err("The bank account the salaries left is required — it is the credit side of the payment journal.");

        var run = await runs.GetByIdAsync(file.PayrollRunId);
        if (run is null) return Err("The file's payroll run no longer exists.");

        var accounts = await finance.ListAccountsAsync(ct);
        if (accounts.Count == 0)
            return Err("Finance's chart of accounts could not be read, so the payment journal cannot be posted. Try again once finance is reachable.");

        var bankAccount = accounts.FirstOrDefault(a => a.Id == dto.BankGlAccountId);
        if (bankAccount is null) return Err("That bank account is not in finance's chart of accounts.");
        if (!bankAccount.IsDirectPosting) return Err($"Account {bankAccount.Code} is a header account and cannot be posted to.");

        var holding = accounts.FirstOrDefault(a => a.Code == GlNetPayHolding);
        if (holding is null)
            return Err($"Account {GlNetPayHolding} (the net-pay holding account) is not in finance's chart of accounts.");

        // The second journal: net pay leaves the holding account it has sat in since approval and leaves the
        // bank. Only the amount this FILE pays — excluded staff are still owed and stay in the holding account.
        var lines = new List<JournalLineDto>
        {
            new(holding.Id, $"{run.RunNumber} — net pay settled ({file.BankName})", file.TotalAmount, 0m),
            new(bankAccount.Id, $"{run.RunNumber} — salaries paid{(string.IsNullOrWhiteSpace(dto.Reference) ? "" : $" ({dto.Reference.Trim()})")}", 0m, file.TotalAmount),
        };

        var posted = await finance.PostJournalAsync(
            tenantSchema ?? string.Empty, DateTime.UtcNow.Date,
            $"Salary payment {run.PayrollPeriodCode} — {file.FileName}", file.Id, lines, ct);

        file.ConfirmedBy = userId;
        file.ConfirmedAt = DateTime.UtcNow;
        file.BankGlAccountId = bankAccount.Id;
        file.BankGlAccountCode = bankAccount.Code;

        if (posted.Posted)
        {
            file.JournalEntryId = posted.JournalEntryId;
            file.JournalEntryNo = posted.JournalEntryNo;
            file.JournalError = null;
        }
        else
        {
            file.JournalError = posted.Message;
        }
        Touch(file, userId);
        await bankFiles.UpdateAsync(file);

        var result = new PayrollActionResult(posted.Posted ? "Confirmed" : "Failed",
            posted.Posted
                ? $"{file.FileName} confirmed — {run.CurrencyCode} {file.TotalAmount:N2} moved from the holding account to {bankAccount.Code} {bankAccount.Name}."
                : $"{file.FileName} was recorded as uploaded, but the payment journal did not post.", file.Id);
        result.Warnings.Add(posted.Message);

        if (file.MissingCount > 0)
            result.Warnings.Add($"{file.MissingCount} employee(s) were not in this file — their net pay is still sitting in the holding account and is still owed.");

        await LogAsync("BankPaymentFile", file.Id,
            posted.Posted ? HrAuditAction.BankFileConfirmed : HrAuditAction.PayrollJournalFailed,
            posted.Posted
                ? $"{file.FileName} confirmed by {userName ?? userId}: {file.TotalAmount:N2} paid from {bankAccount.Code}, journal {posted.JournalEntryNo}."
                : $"{file.FileName} confirmed by {userName ?? userId} but the payment journal failed: {posted.Message}", userId, userName);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Documents (P10, HR-010)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<PayslipDto?> GetPayslipAsync(string id)
    {
        var slip = await payslips.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (slip is null) return null;
        var lines = await payslipLines.Query().AsNoTracking().Where(l => l.PayslipId == id).ToListAsync();
        return ToDto(slip, lines);
    }

    public async Task<P9CertificateDto?> GetP9Async(string employeeId, int year)
    {
        var employee = await employees.Query().AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null) return null;

        // Only APPROVED runs count: a draft or cancelled run is not pay anyone received, and a tax certificate
        // that includes one would overstate what was declared to KRA.
        var approvedRunIds = await runs.Query().AsNoTracking()
            .Where(r => r.Status == PayrollRunStatus.Approved).Select(r => r.Id).ToListAsync();

        var slips = await payslips.Query().AsNoTracking()
            .Where(p => p.EmployeeId == employeeId
                     && p.PayrollPeriodCode.StartsWith(year.ToString("D4"))
                     && approvedRunIds.Contains(p.PayrollRunId))
            .OrderBy(p => p.PayrollPeriodCode).ToListAsync();

        var months = slips.Select(p => new P9MonthDto
        {
            PeriodCode = p.PayrollPeriodCode,
            GrossPay = p.GrossPay,
            TaxableIncome = p.TaxableIncome,
            // Tax charged before relief — relief is shown in its own column, as a P9 does.
            TaxCharged = Round(p.Paye + p.PersonalRelief),
            PersonalRelief = p.PersonalRelief,
            Paye = p.Paye,
            StatutoryDeductions = p.StatutoryDeductions,
        }).ToList();

        return new P9CertificateDto
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            KraPin = employee.KraPin,
            Year = year,
            Months = months,
            TotalGross = Round(months.Sum(m => m.GrossPay)),
            TotalTaxable = Round(months.Sum(m => m.TaxableIncome)),
            TotalTaxCharged = Round(months.Sum(m => m.TaxCharged)),
            TotalRelief = Round(months.Sum(m => m.PersonalRelief)),
            TotalPaye = Round(months.Sum(m => m.Paye)),
            TotalStatutory = Round(months.Sum(m => m.StatutoryDeductions)),
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>Quotes a CSV field when it contains anything that would break the row. Staff names carry
    /// commas and apostrophes more often than people expect.</summary>
    private static string Csv(string? value)
    {
        var v = value ?? string.Empty;
        return v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r')
            ? $"\"{v.Replace("\"", "\"\"")}\""
            : v;
    }

    private static BankPaymentFileDto ToDto(BankPaymentFile f) => new()
    {
        Id = f.Id, PayrollRunId = f.PayrollRunId, PayrollPeriodCode = f.PayrollPeriodCode,
        Format = f.Format.ToString(), BankName = f.BankName, FileName = f.FileName,
        EmployeeCount = f.EmployeeCount, TotalAmount = f.TotalAmount,
        MissingCount = f.MissingCount,
        MissingBankDetails = string.IsNullOrWhiteSpace(f.MissingBankDetails) ? [] : f.MissingBankDetails.Split(" | ").ToList(),
        NeedsFormatConfirmation = f.NeedsFormatConfirmation,
        GeneratedBy = f.GeneratedBy, GeneratedAt = f.GeneratedAt, DownloadedAt = f.DownloadedAt,
        ConfirmedBy = f.ConfirmedBy, ConfirmedAt = f.ConfirmedAt,
        BankGlAccountCode = f.BankGlAccountCode, JournalEntryNo = f.JournalEntryNo, JournalError = f.JournalError,
    };

    private static PayslipDto ToDto(Payslip p, List<PayslipLine> lines) => new()
    {
        Id = p.Id, PayrollRunId = p.PayrollRunId, PayrollPeriodCode = p.PayrollPeriodCode,
        EmployeeId = p.EmployeeId, EmployeeNumber = p.EmployeeNumber, EmployeeName = p.EmployeeName,
        DepartmentName = p.DepartmentName, KraPin = p.KraPin, SalaryStructureName = p.SalaryStructureName,
        BasicSalary = p.BasicSalary, CurrencyCode = p.CurrencyCode,
        GrossPay = p.GrossPay, TaxableIncome = p.TaxableIncome, Paye = p.Paye, PersonalRelief = p.PersonalRelief,
        StatutoryDeductions = p.StatutoryDeductions, OtherDeductions = p.OtherDeductions,
        TotalEarnings = p.TotalEarnings, TotalDeductions = p.TotalDeductions,
        NetPay = p.NetPay, EmployerCost = p.EmployerCost,
        OvertimeHours = p.OvertimeHours, OvertimePay = p.OvertimePay,
        UnpaidDays = p.UnpaidDays, UnpaidDeduction = p.UnpaidDeduction,
        PdfUrl = p.PdfUrl, EmailedAt = p.EmailedAt, ViewedAt = p.ViewedAt,
        Lines = lines.OrderBy(l => l.LineOrder).Select(l => new PayslipLineDto
        {
            Id = l.Id, Code = l.Code, Name = l.Name, LineType = l.LineType.ToString(),
            Amount = l.Amount, LineOrder = l.LineOrder, Basis = l.Basis,
            IsTaxable = l.IsTaxable, IsStatutory = l.IsStatutory,
            GlAccountCode = l.GlAccountCode, GlAccountName = l.GlAccountName,
        }).ToList(),
    };

    private async Task NotifyAsync(string? schema, string severity, string title, string message, string permission)
    {
        if (string.IsNullOrWhiteSpace(schema)) return;
        await notifier.CreateAlertAsync(schema, "HR", severity, title, message, permission);
    }

    private static decimal Round(decimal amount) => Money.Round(amount);
    private static PayrollActionResult Err(string message) => new("Error", message);
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }

    private async Task LogAsync(string entityType, string entityId, HrAuditAction action, string detail, string userId, string? userName = null)
    {
        await audit.CreateAsync(new HrAuditLog
        {
            EntityType = entityType, EntityId = entityId, Action = action, Detail = detail,
            PerformedBy = userId, PerformedByName = userName, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
