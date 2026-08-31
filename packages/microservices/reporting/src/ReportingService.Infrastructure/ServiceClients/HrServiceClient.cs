using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.ServiceClients;

/// <summary>
/// The eighth service client, built to the same shape as the other seven — BaseServiceClient carries the
/// JWT forwarding, the timeout and the error handling, so this is only routes and types.
///
/// <para>One difference that matters: both HR endpoints return a bare <c>{ "data": ... }</c> with no
/// <c>success</c> field (<c>Ok(new { data = ... })</c>), so these read through
/// <c>GetUnwrappedAsync</c>. Using the usual <c>GetAsync</c> returns null on a perfectly good 200 —
/// see the note on that method.</para>
///
/// <para>Both routes require HR permissions of their own — <c>hr.payroll.read</c> and
/// <c>hr.read.dept</c> — and this client forwards the caller's own token, so <c>reports.view</c> alone
/// is not enough to read either report. That is deliberate: payroll figures should not become readable
/// by way of a reporting role. A caller without them gets a warning on the report rather than data.</para>
/// </summary>
public class HrServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HrServiceClient> logger)
    : BaseServiceClient(httpClientFactory, config, httpContextAccessor, logger), IHrServiceClient
{
    protected override string ClientName => "HrService";
    protected override string ConfigKey => "HrService";

    public Task<List<PayrollRunRowDto>?> GetPayrollRunsAsync(string? status) =>
        GetUnwrappedAsync<List<PayrollRunRowDto>>(
            $"/api/v1/hr/payroll/runs{BuildQuery(new Dictionary<string, string?> { ["status"] = status })}");

    public Task<List<LeaveEntitlementRowDto>?> GetLeaveEntitlementsAsync(int? year) =>
        GetUnwrappedAsync<List<LeaveEntitlementRowDto>>(
            $"/api/v1/hr/leave/entitlements{BuildQuery(new Dictionary<string, string?> { ["year"] = year?.ToString() })}");
}
