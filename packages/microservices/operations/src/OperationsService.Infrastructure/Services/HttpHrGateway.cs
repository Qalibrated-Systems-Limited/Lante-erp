using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Infrastructure.Services;

/// <summary>
/// PR2 — REAL HR seam. Verifies overtime pre-approvals against HR's register and posts negligence
/// recoveries as payroll deductions. Mints a per-schema service token so hr-service scopes to the same
/// tenant, as the O1/O6 CRM client does.
///
/// <para>Config-gated on <c>Hr:Enabled</c> + <c>HrService:BaseUrl</c>; DI falls back to
/// <see cref="NoOpHrGateway"/> when either is unset.</para>
/// </summary>
public class HttpHrGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpHrGateway> logger) : IHrGateway
{
    private string? BaseUrl => config["HrService:BaseUrl"]?.TrimEnd('/');
    private string? Schema => httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;

    /// <summary>Deduction type the negligence recovery posts against (HR's seeded catalogue).</summary>
    private const string RecoveryDeductionCode = "ADVANCE";

    /// <summary>
    /// Reads HR's overtime register for this employee and date and reports whether an approved record
    /// covers the claimed hours. Never creates one — see <see cref="IHrGateway.ResolveOvertimeApprovalAsync"/>
    /// for why the ops timesheet must not raise a retrospective pre-approval.
    /// </summary>
    public async Task<OvertimeApprovalResult> ResolveOvertimeApprovalAsync(
        OvertimeApprovalQuery query, CancellationToken ct = default)
    {
        // GET /hr/overtime sits behind hr.read.dept, not a payroll policy — the manager reading their
        // department's overtime is the intended caller.
        var client = await AuthorizedAsync("hr.read.dept", "hr.payroll.read");
        if (client is null)
        {
            // Seam off: fall back to the stub's permissive behaviour so timesheets are not blocked by
            // HR being absent, but say so plainly rather than implying a real approval exists.
            logger.LogInformation("[HR seam (disabled)] overtime for entry {Entry} accepted without verification.",
                query.TimesheetEntryId);
            return new OvertimeApprovalResult(true, $"OT-UNVERIFIED-{query.TimesheetEntryId}",
                "HR integration is disabled — overtime was accepted without verification.");
        }

        try
        {
            var day = query.WorkDate.Date;
            var url = $"{BaseUrl}/api/v1/hr/overtime" +
                      $"?employeeId={Uri.EscapeDataString(query.EmployeeId)}" +
                      $"&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}";

            var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("HR overtime lookup for entry {Entry} returned {Status}.",
                    query.TimesheetEntryId, resp.StatusCode);
                return new OvertimeApprovalResult(false, null,
                    "Could not check the overtime approval with HR. Try again shortly.");
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return new OvertimeApprovalResult(false, null, "HR returned no overtime records for that day.");

            // The date filter is a range, so confirm the day on each row rather than trusting it.
            foreach (var row in arr.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) continue;

                if (row.TryGetProperty("date", out var dEl) &&
                    dEl.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(dEl.GetString(), out var rowDate) &&
                    rowDate.Date != day)
                    continue;

                var status = row.TryGetProperty("status", out var sEl) ? sEl.GetString() : null;
                if (!string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)) continue;

                var approvedHours = row.TryGetProperty("hours", out var hEl) && hEl.TryGetDecimal(out var h) ? h : (decimal?)null;
                var reference = row.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

                // An approval for fewer hours than claimed is not an approval for the larger figure —
                // that is exactly the overrun the pre-approval control exists to catch.
                if (approvedHours is not null && approvedHours < query.OvertimeHours)
                    return new OvertimeApprovalResult(false, reference,
                        $"HR approved {approvedHours:0.##}h of overtime for {day:dd MMM yyyy}, but this entry claims {query.OvertimeHours:0.##}h.",
                        approvedHours);

                logger.LogInformation("Overtime for entry {Entry} verified against HR record {Ref}.",
                    query.TimesheetEntryId, reference);
                return new OvertimeApprovalResult(true, reference, null, approvedHours);
            }

            return new OvertimeApprovalResult(false, null,
                $"No approved overtime found in HR for {day:dd MMM yyyy}. Overtime must be approved by your manager before it is worked.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HR overtime lookup for entry {Entry} failed.", query.TimesheetEntryId);
            return new OvertimeApprovalResult(false, null, "Could not reach the HR service to check the overtime approval.");
        }
    }

    /// <summary>
    /// Intentionally does nothing but log — HR payroll takes salaries, not hours. See the note on
    /// <see cref="IHrGateway.PostTimesheetToPayrollAsync"/>.
    /// </summary>
    public Task PostTimesheetToPayrollAsync(PayrollPosting posting, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[HR payroll — nothing to post] employee {Emp} week-ending {Week:yyyy-MM-dd}: {Total}h ({OT}h OT). " +
            "Payroll is salary-based; overtime reaches payroll through its own approved HR record.",
            posting.EmployeeId, posting.WeekEndDate, posting.TotalHours, posting.OvertimeHours);
        return Task.CompletedTask;
    }

    public async Task PostPayrollDeductionAsync(PayrollDeduction deduction, CancellationToken ct = default)
    {
        var client = await AuthorizedAsync("hr.payroll.write");
        if (client is null)
        {
            logger.LogInformation("[HR seam (disabled)] payroll deduction: employee {Emp} {Amount:0.00} ({Ref}).",
                deduction.EmployeeId, deduction.Amount, deduction.IncidentReference);
            return;
        }

        try
        {
            var typeId = await ResolveDeductionTypeIdAsync(client, ct);
            if (typeId is null)
            {
                logger.LogWarning("No '{Code}' deduction type in HR — deduction for {Ref} not posted.",
                    RecoveryDeductionCode, deduction.IncidentReference);
                return;
            }

            var body = new
            {
                employeeId = deduction.EmployeeId,
                deductionTypeId = typeId,
                amount = deduction.Amount,
                notes = $"Operations negligence recovery {deduction.IncidentReference}: {deduction.Reason}",
            };

            var resp = await client.PostAsync($"{BaseUrl}/api/v1/hr/payroll/deductions",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("HR deduction post for {Ref} returned {Status}: {Body}",
                    deduction.IncidentReference, resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
            else
                logger.LogInformation("Posted payroll deduction {Amount:0.00} for employee {Emp} ({Ref}).",
                    deduction.Amount, deduction.EmployeeId, deduction.IncidentReference);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HR deduction post for {Ref} failed (best-effort).", deduction.IncidentReference);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<string?> ResolveDeductionTypeIdAsync(HttpClient client, CancellationToken ct)
    {
        var resp = await client.GetAsync($"{BaseUrl}/api/v1/hr/payroll/deduction-types", ct);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("data", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var t in arr.EnumerateArray())
            if (t.ValueKind == JsonValueKind.Object &&
                t.TryGetProperty("code", out var c) &&
                string.Equals(c.GetString(), RecoveryDeductionCode, StringComparison.OrdinalIgnoreCase))
                return t.TryGetProperty("id", out var id) ? id.GetString() : null;

        return null;
    }

    /// <summary>Null when the seam is off or the tenant schema cannot be resolved.</summary>
    private async Task<HttpClient?> AuthorizedAsync(params string[] permissions)
    {
        if (!config.GetValue("Hr:Enabled", false) || string.IsNullOrWhiteSpace(BaseUrl)) return null;

        var schema = Schema;
        if (string.IsNullOrWhiteSpace(schema))
        {
            logger.LogWarning("HR call skipped — no tenant schema on the request.");
            return null;
        }

        var client = httpClientFactory.CreateClient("HrService");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, ["system.admin", .. permissions]));
        client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);
        return client;
    }
}
