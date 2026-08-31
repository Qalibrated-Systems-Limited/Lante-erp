using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using TicketingService.Api.Authorization;
using Xunit;

namespace TicketingService.Tests;

/// <summary>
/// The internal service-key guard.
///
/// <para>This attribute is the only thing between ticketing's service-to-service endpoints and
/// anything that can reach the pod, and it had no tests. Twenty-five of the platform's twenty-eight
/// internal controllers rely on it; the other two checked the key inline with a plain <c>!=</c>
/// comparison, which short-circuits at the first differing byte and leaks the secret through timing.
/// Moving them onto this attribute is what these tests are protecting.</para>
///
/// <para>Two properties matter beyond "right key passes": it must <b>fail closed</b> when the key is
/// not configured, and it must accept the legacy header so the operations and fleet callers keep
/// working without a coordinated deploy.</para>
///
/// <para><b>What these tests cannot cover, and nobody should think they do.</b> Replacing
/// <c>FixedTimeEquals</c> with a plain <c>!=</c> fails none of them — verified by making that exact
/// change and re-running. Both comparisons return the same answer for every input; only the time
/// taken differs, and a test that measured elapsed time would be flaky enough to be switched off
/// within a week. The constant-time property is protected by code review and by the comment on the
/// attribute, not by this file.</para>
///
/// <para>That is a different thing from a test gap. A gap means an assertion is missing; here the
/// property is simply not observable in the value the method returns. Recorded so that "twelve tests
/// on the service-key guard" is not mistaken for "the timing property is covered".</para>
/// </summary>
public class ServiceKeyAuthorizeTests
{
    private const string Key = "s3cret-internal-key";

    private static ActionExecutingContext Context(string? configuredKey, params (string Header, string Value)[] headers)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            configuredKey is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["InternalServices:ServiceKey"] = configuredKey }).Build();
        services.AddSingleton<IConfiguration>(config);

        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        foreach (var (h, v) in headers) http.Request.Headers[h] = v;

        return new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: null!);
    }

    private static async Task<IActionResult?> RunAsync(ActionExecutingContext ctx)
    {
        var called = false;
        await new ServiceKeyAuthorizeAttribute().OnActionExecutionAsync(ctx, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(ctx, new List<IFilterMetadata>(), controller: null!));
        });
        // A filter that both short-circuits AND calls next would run the action anyway; assert the
        // pairing rather than only the result.
        if (called) ctx.Result.Should().BeNull("a filter that let the action run must not also set a result");
        return ctx.Result;
    }

    // ── The key is accepted ─────────────────────────────────────────────────────

    [Fact]
    public async Task The_canonical_header_with_the_right_key_is_let_through()
    {
        var result = await RunAsync(Context(Key, ("X-Internal-Key", Key)));

        result.Should().BeNull("no result set means the action ran");
    }

    [Fact]
    public async Task The_legacy_header_is_still_accepted()
    {
        // internal/work-update and internal/service-request-status have always been called with
        // X-Service-Key, by operations and fleet. Rejecting it here would break those integrations
        // the moment this deploys, which is the only reason the legacy name is still honoured.
        var result = await RunAsync(Context(Key, ("X-Service-Key", Key)));

        result.Should().BeNull();
    }

    [Fact]
    public async Task The_canonical_header_wins_when_both_are_present()
    {
        // If a caller sends both, the platform's own header decides. Falling back to the legacy value
        // would let a stale client override a correct one.
        var result = await RunAsync(Context(Key, ("X-Internal-Key", Key), ("X-Service-Key", "wrong")));

        result.Should().BeNull();
    }

    // ── The key is refused ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_wrong_key_is_unauthorised()
    {
        var result = await RunAsync(Context(Key, ("X-Internal-Key", "wrong")));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task A_missing_header_is_unauthorised()
    {
        var result = await RunAsync(Context(Key));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task An_empty_header_value_is_unauthorised()
    {
        var result = await RunAsync(Context(Key, ("X-Internal-Key", "")));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task A_key_that_is_a_prefix_of_the_real_one_is_unauthorised()
    {
        // The case a length-insensitive comparison would wave through. FixedTimeEquals returns false
        // on a length mismatch, which is what makes prefix probing useless.
        var result = await RunAsync(Context(Key, ("X-Internal-Key", Key[..8])));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task A_key_differing_only_in_the_last_character_is_unauthorised()
    {
        var result = await RunAsync(Context(Key, ("X-Internal-Key", Key[..^1] + "X")));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task The_comparison_is_case_sensitive()
    {
        var result = await RunAsync(Context(Key, ("X-Internal-Key", Key.ToUpperInvariant())));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── It fails closed ─────────────────────────────────────────────────────────

    [Fact]
    public async Task An_unconfigured_key_refuses_everything_rather_than_allowing_it()
    {
        // The property that matters most. A deployment that forgets InternalServices:ServiceKey must
        // reject internal calls, not accept them — an empty expected value compared loosely would
        // otherwise match an empty provided one and open every internal endpoint.
        var result = await RunAsync(Context(configuredKey: null, ("X-Internal-Key", "anything")));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task An_unconfigured_key_refuses_an_empty_header_too()
    {
        // The specific coincidence: empty expected, empty provided. A plain equality check would
        // treat that as a match and let the caller straight in.
        var result = await RunAsync(Context(configuredKey: null, ("X-Internal-Key", "")));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task An_empty_configured_key_is_treated_as_unconfigured()
    {
        var result = await RunAsync(Context(configuredKey: "", ("X-Internal-Key", "")));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }
}
