using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Payroll;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>
/// H6 (HR-007/008/012, P9 + P12) — overtime pre-approval and the monthly payroll run.
/// <para>Overtime sits at <c>hr.manager</c> because the first approver is the line manager, exactly as leave
/// does. Everything to do with the run itself is on the ring-fenced payroll tier, and <b>approving a run needs
/// <c>hr.payroll.approve</c></b> — plus a second officer, since the service refuses to let whoever computed a
/// run sign it off.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr")]
[Authorize]
public class PayrollRunController(
    IPayrollRunService service,
    IPayrollDocumentService documents,
    HrService.Api.Services.PayrollPdfService pdf,
    IConfiguration config) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");
    private string? Schema => User.FindFirstValue("schema");

    // ── Overtime (P12) ──
    [HttpGet("overtime")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListOvertime(
        [FromQuery] string? employeeId, [FromQuery] string? status,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(new { data = await service.ListOvertimeAsync(employeeId, status, from, to) });

    /// <summary>Pre-approval only — overtime must be authorised before it is worked (ATT-007).</summary>
    [HttpPost("overtime")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> RequestOvertime([FromBody] RequestOvertimeDto dto)
        => Act(await service.RequestOvertimeAsync(dto, UserId));

    [HttpPost("overtime/{id}/decide")]
    [Authorize(Policy = "Permission:hr.manager")]
    public async Task<IActionResult> DecideOvertime(string id, [FromBody] DecideOvertimeDto dto)
        => Act(await service.DecideOvertimeAsync(id, dto, UserId, UserName));

    // ── Payroll runs (P9) ──
    [HttpGet("payroll/runs")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListRuns([FromQuery] string? status)
        => Ok(new { data = await service.ListRunsAsync(status) });

    [HttpGet("payroll/runs/{id}")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> GetRun(string id)
    {
        var run = await service.GetRunAsync(id);
        return run is null ? NotFound(new { message = "Payroll run not found." }) : Ok(new { data = run });
    }

    [HttpPost("payroll/runs")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> CreateRun([FromBody] CreatePayrollRunDto? dto)
        => Act(await service.CreateRunAsync(dto ?? new CreatePayrollRunDto(), UserId));

    /// <summary>Builds every payslip. Safe to repeat while the run is unapproved — it releases what it
    /// previously claimed before recomputing.</summary>
    [HttpPost("payroll/runs/{id}/compute")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> ComputeRun(string id)
        => Act(await service.ComputeRunAsync(id, UserId));

    /// <summary>The journal this run would post, so it can be checked before approval commits it.</summary>
    [HttpGet("payroll/runs/{id}/journal-preview")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> PreviewJournal(string id, CancellationToken ct)
    {
        var preview = await service.PreviewJournalAsync(id, ct);
        return preview is null ? NotFound(new { message = "Payroll run not found." }) : Ok(new { data = preview });
    }

    /// <summary>Approve (posts the payroll journal) or cancel (releases every claim the run held).</summary>
    [HttpPost("payroll/runs/{id}/decide")]
    [Authorize(Policy = "Permission:hr.payroll.approve")]
    public async Task<IActionResult> DecideRun(string id, [FromBody] DecidePayrollRunDto dto, CancellationToken ct)
        => Act(await service.DecideRunAsync(id, dto, Schema, UserId, UserName, ct));

    /// <summary>Re-posts the journal for an approved run whose finance hop failed.</summary>
    [HttpPost("payroll/runs/{id}/post-journal")]
    [Authorize(Policy = "Permission:hr.payroll.approve")]
    public async Task<IActionResult> RetryJournal(string id, CancellationToken ct)
        => Act(await service.RetryJournalAsync(id, Schema, UserId, ct));

    // ── Payslips (P10 — data only in this pass; the PDF comes with the documents pass) ──
    [HttpGet("payroll/payslips")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListPayslips(
        [FromQuery] string? runId, [FromQuery] string? employeeId, [FromQuery] int? year)
        => Ok(new { data = await service.ListPayslipsAsync(runId, employeeId, year) });

    /// <summary>The payslip PDF, rendered on request from the frozen payslip lines (P10 step 10.2).</summary>
    [HttpGet("payroll/payslips/{id}/pdf")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> PayslipPdf(string id)
    {
        var slip = await documents.GetPayslipAsync(id);
        if (slip is null) return NotFound(new { message = "Payslip not found." });
        var bytes = pdf.GeneratePayslip(slip, CompanyName);
        return File(bytes, "application/pdf", $"payslip-{slip.PayrollPeriodCode}-{slip.EmployeeNumber}.pdf");
    }

    // ── P9 annual tax certificate (HR-010) ──
    [HttpGet("payroll/p9")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> P9([FromQuery] string employeeId, [FromQuery] int year)
    {
        var cert = await documents.GetP9Async(employeeId, year);
        return cert is null ? NotFound(new { message = "Employee not found." }) : Ok(new { data = cert });
    }

    [HttpGet("payroll/p9/pdf")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> P9Pdf([FromQuery] string employeeId, [FromQuery] int year)
    {
        var cert = await documents.GetP9Async(employeeId, year);
        if (cert is null) return NotFound(new { message = "Employee not found." });
        var bytes = pdf.GenerateP9(cert, CompanyName);
        return File(bytes, "application/pdf", $"P9-{cert.Year}-{cert.EmployeeNumber}.pdf");
    }

    // ── Bank payment files (P11) ──
    [HttpGet("payroll/bank-files")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListBankFiles([FromQuery] string? runId)
        => Ok(new { data = await documents.ListBankFilesAsync(runId) });

    [HttpPost("payroll/runs/{id}/bank-file")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> GenerateBankFile(string id, [FromBody] GenerateBankFileDto? dto)
        => Act(await documents.GenerateBankFileAsync(id, dto ?? new GenerateBankFileDto(), Schema, UserId));

    [HttpGet("payroll/bank-files/{id}/download")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> DownloadBankFile(string id)
    {
        var file = await documents.DownloadBankFileAsync(id, UserId);
        if (file is null) return NotFound(new { message = "Bank payment file not found." });
        return File(System.Text.Encoding.UTF8.GetBytes(file.Value.Content), "text/csv", file.Value.FileName);
    }

    /// <summary>Records that the file reached the bank and posts the payment journal (P11 step 11.5).
    /// Needs the approver tier — it is the point at which the money is treated as gone.</summary>
    [HttpPost("payroll/bank-files/{id}/confirm")]
    [Authorize(Policy = "Permission:hr.payroll.approve")]
    public async Task<IActionResult> ConfirmBankFile(string id, [FromBody] ConfirmBankFileDto dto, CancellationToken ct)
        => Act(await documents.ConfirmBankFileAsync(id, dto, Schema, UserId, UserName, ct));

    /// <summary>Tenant display name for the document header. Falls back rather than failing to render.</summary>
    private string CompanyName => config["Tenant:CompanyName"] ?? "Lante";

    private IActionResult Act(PayrollActionResult r)
        // `code` is emitted only when the service set one. The UI branches on it rather than on the message
        // text, so rewording a refusal cannot silently disable the acknowledgement path (#244).
        => r.Status == "Error" ? BadRequest(new { message = r.Message, code = r.Code }) : Ok(new { data = r });
}
