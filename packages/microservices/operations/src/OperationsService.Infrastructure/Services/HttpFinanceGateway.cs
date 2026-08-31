using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Infrastructure.Services;

/// <summary>
/// PR2 — REAL finance seam. Milestone sign-off and applied variation orders create DRAFT AR invoices;
/// approved timesheets post a labour journal against project actuals. Mints a per-schema service token
/// so finance-service scopes to the same tenant, exactly as the O1/O6 CRM client does.
///
/// <para>Every method is best-effort and swallows its own failures: none of these hops may void the
/// operation that triggered them — a milestone is signed off whether or not finance is reachable.
/// Config-gated on <c>Finance:Enabled</c> + <c>FinanceService:BaseUrl</c>; DI falls back to
/// <see cref="NoOpFinanceGateway"/> when either is unset.</para>
/// </summary>
public class HttpFinanceGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpFinanceGateway> logger) : IFinanceGateway
{
    private string? BaseUrl => config["FinanceService:BaseUrl"]?.TrimEnd('/');
    private string? Schema => httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;

    // Reclassification pair for project labour. Payroll has already expensed these people to 5200
    // Salaries & Wages, so debiting a cost account again would double-count the same shilling in the
    // P&L. Moving the cost from 5200 to 5100 Cost of Services leaves the profit figure untouched and
    // puts project labour where project reporting expects to find it.
    private const string LabourCostAccount   = "5100";   // Cost of Services (Dr)
    private const string LabourSourceAccount = "5200";   // Salaries & Wages (Cr)

    public Task RaiseMilestoneInvoiceAsync(MilestoneInvoiceRequest request, CancellationToken ct = default) =>
        RaiseInvoiceAsync(
            projectId: request.ProjectId,
            clientId: request.ClientId,
            clientName: request.ClientName,
            amount: request.Amount,
            sourceDocumentId: request.MilestoneId,
            milestoneId: request.MilestoneId,
            lineDescription: $"Milestone — {request.MilestoneTitle}",
            notes: $"Auto-created on sign-off of milestone '{request.MilestoneTitle}'.",
            label: $"milestone {request.MilestoneId}",
            ct);

    public Task RaiseVariationInvoiceAsync(VariationInvoiceRequest request, CancellationToken ct = default) =>
        RaiseInvoiceAsync(
            projectId: request.ProjectId,
            clientId: request.ClientId,
            clientName: request.ClientName,
            amount: request.Amount,
            sourceDocumentId: request.VariationOrderId,
            milestoneId: null,
            lineDescription: $"Variation order {request.VariationOrderNumber}",
            notes: $"Auto-created on application of variation order {request.VariationOrderNumber}.",
            label: $"variation order {request.VariationOrderNumber}",
            ct);

    private async Task RaiseInvoiceAsync(
        string projectId, string? clientId, string? clientName, decimal amount,
        string sourceDocumentId, string? milestoneId, string lineDescription,
        string notes, string label, CancellationToken ct)
    {
        var client = await AuthorizedAsync("finance.write");
        if (client is null)
        {
            logger.LogInformation("[Finance seam (disabled)] invoice for {Label}: {Amount:0.00}.", label, amount);
            return;
        }

        try
        {
            var customerId = await ResolveOrCreateCustomerAsync(client, clientId, clientName, projectId, ct);
            if (customerId is null)
            {
                logger.LogWarning("Could not resolve a finance customer for {Label} — invoice not raised.", label);
                return;
            }

            var body = new
            {
                customerId,
                invoiceDate = DateTime.UtcNow,
                milestoneId,
                sourceModule = "Operations",
                sourceDocumentId,
                notes,
                lines = new[]
                {
                    new
                    {
                        description = lineDescription,
                        quantity = 1m,
                        unitPrice = amount,
                        // Exempt: the milestone/VO figure is the agreed contract amount and already
                        // carries whatever VAT was quoted. Taxing it again would inflate the invoice.
                        taxCode = "E",
                    },
                },
            };

            var resp = await client.PostAsync($"{BaseUrl}/api/v1/finance/invoices",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Finance invoice create for {Label} returned {Status}: {Body}",
                    label, resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
                return;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var data = doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : default;
            var invoiceNo = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("invoiceNo", out var no) ? no.GetString() : null;
            logger.LogInformation("Raised finance draft invoice {No} for {Label} ({Amount:0.00}).", invoiceNo ?? "?", label, amount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Finance invoice create for {Label} failed (best-effort).", label);
        }
    }

    public async Task PostTimesheetLabourAsync(TimesheetLabourPosting posting, CancellationToken ct = default)
    {
        // Priced by the caller from the project rate card. Nothing to post if the hours could not be
        // priced — a zero-value journal is noise, and an invented rate is a wrong number in the GL.
        if (posting.Amount <= 0m)
        {
            logger.LogInformation(
                "Labour posting for project {Project} week-ending {Week:yyyy-MM-dd} skipped — {Hours}h could not be priced from the rate card.",
                posting.ProjectId, posting.WeekEndDate, posting.Hours + posting.OvertimeHours);
            return;
        }

        var client = await AuthorizedAsync("finance.write");
        if (client is null)
        {
            logger.LogInformation("[Finance seam (disabled)] labour journal: project {Project} {Amount:0.00}.",
                posting.ProjectId, posting.Amount);
            return;
        }

        try
        {
            var description =
                $"Project labour — {posting.Hours:0.##}h" +
                (posting.OvertimeHours > 0m ? $" + {posting.OvertimeHours:0.##}h OT" : "") +
                $", employee {posting.EmployeeId}, week ending {posting.WeekEndDate:yyyy-MM-dd}";

            var body = new
            {
                entryDate = posting.WeekEndDate,
                description,
                sourceModule = "Operations",
                // Employee + week is the natural key of this posting; it keeps a re-approval of the
                // same timesheet traceable to the entry it already produced.
                sourceDocumentId = $"TS:{posting.ProjectId}:{posting.EmployeeId}:{posting.WeekEndDate:yyyyMMdd}",
                postImmediately = true,   // inbound module post — not a journal anyone reviews by hand
                lines = new object[]
                {
                    new
                    {
                        accountCode = LabourCostAccount,
                        description = $"Labour to project {posting.ProjectId}" + (posting.RateCode is null ? "" : $" @ {posting.RateCode}"),
                        debit = posting.Amount,
                        credit = 0m,
                    },
                    new
                    {
                        accountCode = LabourSourceAccount,
                        description = "Reclassified from salaries & wages",
                        debit = 0m,
                        credit = posting.Amount,
                    },
                },
            };

            var resp = await client.PostAsync($"{BaseUrl}/api/v1/finance/journals",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("Finance labour journal for project {Project} returned {Status}: {Body}",
                    posting.ProjectId, resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
            else
                logger.LogInformation("Posted labour journal {Amount:0.00} for project {Project} week-ending {Week:yyyy-MM-dd}.",
                    posting.Amount, posting.ProjectId, posting.WeekEndDate);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Finance labour journal for project {Project} failed (best-effort).", posting.ProjectId);
        }
    }

    /// <summary>
    /// Logged, never posted — finance-service has no revenue-recognition feature to post to. See the
    /// note on <see cref="IFinanceGateway.ReportRevenueRecognitionAsync"/>.
    /// </summary>
    public Task ReportRevenueRecognitionAsync(RevenueRecognitionRequest request, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[Finance revenue recognition — no endpoint] project {Project} milestone {Milestone} {Pct}% → {Amount:0.00}. " +
            "finance-service has no IFRS-15 schedule; nothing was posted.",
            request.ProjectId, request.MilestoneId, request.PercentComplete, request.RecognizedAmount);
        return Task.CompletedTask;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>Null when the seam is off or the tenant schema cannot be resolved.</summary>
    private async Task<HttpClient?> AuthorizedAsync(string permission)
    {
        if (!config.GetValue("Finance:Enabled", false) || string.IsNullOrWhiteSpace(BaseUrl)) return null;

        var schema = Schema;
        if (string.IsNullOrWhiteSpace(schema))
        {
            logger.LogWarning("Finance call skipped — no tenant schema on the request.");
            return null;
        }

        var client = httpClientFactory.CreateClient("FinanceService");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", permission));
        client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);
        return client;
    }

    /// <summary>
    /// Finds the finance AR customer for this project's client, creating a minimal one if absent.
    /// Matches on the <c>CRM-{crmCustomerId}</c> code first — the same convention CRM's own invoice
    /// gateway writes — so a client invoiced from CRM and from operations lands on one finance
    /// customer rather than two. Falls back to an exact name match for clients predating the code.
    /// </summary>
    private async Task<string?> ResolveOrCreateCustomerAsync(
        HttpClient client, string? crmCustomerId, string? clientName, string projectId, CancellationToken ct)
    {
        var name = clientName?.Trim();
        string? code = null;
        if (!string.IsNullOrWhiteSpace(crmCustomerId))
        {
            code = "CRM-" + crmCustomerId;
            if (code.Length > 20) code = code[..20];
        }

        var listResp = await client.GetAsync($"{BaseUrl}/api/v1/finance/customers", ct);
        if (listResp.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("data", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                var items = arr.EnumerateArray().ToList();

                if (code is not null)
                    foreach (var c in items)
                        if (c.TryGetProperty("code", out var cd) &&
                            string.Equals(cd.GetString(), code, StringComparison.OrdinalIgnoreCase))
                            return c.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

                if (!string.IsNullOrWhiteSpace(name))
                    foreach (var c in items)
                        if (c.TryGetProperty("name", out var n) &&
                            string.Equals(n.GetString(), name, StringComparison.OrdinalIgnoreCase))
                            return c.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            }
        }

        // Nothing to create a sensible customer from — better no invoice than one billed to "Unknown".
        if (string.IsNullOrWhiteSpace(name)) return null;

        // No CRM id: derive a stable code from the project so repeat invoices for the same unanchored
        // client reuse one finance customer instead of creating a fresh one each sign-off.
        var newCode = code ?? $"OPS-{projectId}";
        if (newCode.Length > 20) newCode = newCode[..20];

        var createResp = await client.PostAsync($"{BaseUrl}/api/v1/finance/customers",
            new StringContent(JsonSerializer.Serialize(new { code = newCode, name, isActive = true }),
                Encoding.UTF8, "application/json"), ct);
        if (!createResp.IsSuccessStatusCode)
        {
            logger.LogWarning("Finance customer create for '{Name}' returned {Status}.", name, createResp.StatusCode);
            return null;
        }

        using var cdoc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync(ct));
        return cdoc.RootElement.TryGetProperty("data", out var cd2) && cd2.ValueKind == JsonValueKind.Object
            && cd2.TryGetProperty("id", out var cid) ? cid.GetString() : null;
    }
}
