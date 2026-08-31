using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReportingService.Infrastructure.ServiceClients;
using Xunit;

namespace ReportingService.Tests;

/// <summary>
/// The HR service client's wire contract (#225, reports #12 and #13).
///
/// <para><b>Why this file exists.</b> HrService's two read endpoints return a bare
/// <c>Ok(new { data = ... })</c> — no <c>success</c> field. <c>BaseServiceClient.GetAsync</c>
/// deserialises into <c>ApiResponse&lt;T&gt;</c> and returns <c>wrapper is { Success: true } ? Data :
/// default</c>, so a missing <c>success</c> becomes <c>false</c> and a healthy 200 carrying a full
/// payload arrives as <c>null</c>. Nothing throws, so no warning is recorded and the report renders as a
/// clean success reporting zero payroll cost and zero leave liability. I wrote it that way first; these
/// tests are what caught it, and they exist to stop it coming back the next time someone reaches for the
/// more familiar <c>GetAsync</c>.</para>
///
/// <para>A stub <see cref="HttpMessageHandler"/> rather than a live HrService: the point is the JSON
/// shape and the header forwarding, both of which are decided in this process.</para>
/// </summary>
public class HrServiceClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string? AuthorizationSent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            AuthorizationSent = request.Headers.TryGetValues("Authorization", out var v)
                ? string.Join(",", v)
                : null;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    /// <summary>
    /// Per-instance rather than the framework's <c>HttpContextAccessor</c>, which stores the context in
    /// a static AsyncLocal — that leaks one test's request into the next and made an earlier "no inbound
    /// request" test see a token it never set.
    /// </summary>
    private sealed class StubAccessor(HttpContext? ctx) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get => ctx; set => ctx = value; }
    }

    private static (HrServiceClient Client, StubHandler Handler) Build(
        string body,
        HttpStatusCode status = HttpStatusCode.OK,
        string? bearer = "Bearer caller-token")
    {
        var handler = new StubHandler(status, body);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HrService:BaseUrl"] = "http://lante-hr-service:8080",
            })
            .Build();

        HttpContext? ctx = null;
        if (bearer != null)
        {
            ctx = new DefaultHttpContext();
            ctx.Request.Headers["Authorization"] = bearer;
        }

        return (new HrServiceClient(new StubFactory(handler), config, new StubAccessor(ctx),
            NullLogger<HrServiceClient>.Instance), handler);
    }

    // ── The envelope ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_bare_data_envelope_with_no_success_field_still_yields_the_rows()
    {
        // Exactly what HrService's PayrollRunController emits.
        var (client, _) = Build("""{"data":[{"id":"run-1","totalGross":1000,"employeeCount":7}]}""");

        var runs = await client.GetPayrollRunsAsync("Approved");

        // Reading this through GetAsync returns null here, and null is indistinguishable from "this
        // tenant has never run payroll".
        runs.Should().ContainSingle();
        runs![0].Id.Should().Be("run-1");
        runs[0].TotalGross.Should().Be(1000m);
        runs[0].EmployeeCount.Should().Be(7);
    }

    [Fact]
    public async Task Leave_entitlements_come_back_from_the_same_bare_envelope()
    {
        var (client, _) = Build(
            """{"data":[{"employeeId":"e1","leaveTypeId":"lt-1","daysEntitled":21,"daysPending":4}]}""");

        var rows = await client.GetLeaveEntitlementsAsync(2026);

        rows.Should().ContainSingle();
        rows![0].DaysEntitled.Should().Be(21m);
        // Mirrored late — the first version of the DTO dropped this field, which would have reported
        // committed days as freely bookable.
        rows[0].DaysPending.Should().Be(4m);
    }

    [Fact]
    public async Task An_empty_data_array_is_no_rows_rather_than_null()
    {
        var (client, _) = Build("""{"data":[]}""");

        var runs = await client.GetPayrollRunsAsync("Approved");

        runs.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task A_null_data_property_yields_null_rather_than_throwing()
    {
        var (client, _) = Build("""{"data":null}""");

        var runs = await client.GetPayrollRunsAsync("Approved");

        // The report services treat null as "no rows", so this must arrive rather than throw.
        runs.Should().BeNull();
    }

    [Fact]
    public async Task Field_names_are_matched_case_insensitively()
    {
        // ASP.NET's default camelCases, but the anonymous `new { data = ... }` and any hand-rolled
        // response could arrive Pascal-cased. Both must bind.
        var (client, _) = Build("""{"Data":[{"Id":"run-1","TotalGross":250}]}""");

        var runs = await client.GetPayrollRunsAsync(null);

        runs.Should().ContainSingle();
        runs![0].TotalGross.Should().Be(250m);
    }

    // ── Routes and query building ────────────────────────────────────────────────

    [Fact]
    public async Task The_payroll_route_carries_the_status_filter()
    {
        var (client, handler) = Build("""{"data":[]}""");

        await client.GetPayrollRunsAsync("Approved");

        handler.RequestUri.Should().Be(
            "http://lante-hr-service:8080/api/v1/hr/payroll/runs?status=Approved");
    }

    [Fact]
    public async Task A_null_status_is_omitted_from_the_query_rather_than_sent_as_empty()
    {
        var (client, handler) = Build("""{"data":[]}""");

        await client.GetPayrollRunsAsync(null);

        // `?status=` would be bound as an empty string upstream and filter out every run.
        handler.RequestUri.Should().Be("http://lante-hr-service:8080/api/v1/hr/payroll/runs");
    }

    [Fact]
    public async Task The_leave_route_carries_the_year()
    {
        var (client, handler) = Build("""{"data":[]}""");

        await client.GetLeaveEntitlementsAsync(2026);

        handler.RequestUri.Should().Be(
            "http://lante-hr-service:8080/api/v1/hr/leave/entitlements?year=2026");
    }

    // ── Auth forwarding ──────────────────────────────────────────────────────────

    [Fact]
    public async Task The_callers_own_bearer_token_is_forwarded_verbatim()
    {
        var (client, handler) = Build("""{"data":[]}""", bearer: "Bearer caller-token");

        await client.GetPayrollRunsAsync(null);

        // Both HR routes require permissions of their own (hr.payroll.read, hr.read.dept). Reporting
        // has no identity to substitute, and inventing one would let reports.view launder access to
        // payroll figures.
        handler.AuthorizationSent.Should().Be("Bearer caller-token");
    }

    [Fact]
    public async Task With_no_inbound_request_no_authorization_header_is_invented()
    {
        var (client, handler) = Build("""{"data":[]}""", bearer: null);

        await client.GetPayrollRunsAsync(null);

        handler.AuthorizationSent.Should().BeNull();
    }

    // ── Failure surfaces rather than emptying ────────────────────────────────────

    [Fact]
    public async Task A_403_throws_so_the_report_records_a_warning()
    {
        var (client, _) = Build("""{"message":"forbidden"}""", status: HttpStatusCode.Forbidden);

        var act = () => client.GetPayrollRunsAsync("Approved");

        // The distinction that matters: a caller lacking hr.payroll.read must see a warning, not a
        // report stating that payroll cost nothing.
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task A_500_throws_rather_than_returning_empty()
    {
        var (client, _) = Build("", status: HttpStatusCode.InternalServerError);

        var act = () => client.GetLeaveEntitlementsAsync(2026);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task A_missing_base_url_fails_loudly_at_the_first_call()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"data":[]}""");
        var client = new HrServiceClient(
            new StubFactory(handler),
            new ConfigurationBuilder().Build(),
            new StubAccessor(new DefaultHttpContext()),
            NullLogger<HrServiceClient>.Instance);

        var act = () => client.GetPayrollRunsAsync(null);

        // HrService__BaseUrl is a new environment variable on the reporting deployment. If the helm
        // change is missed, this is the error, and it names the key rather than surfacing as a
        // confusing relative-URI failure.
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*HrService:BaseUrl*");
    }
}
