using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TicketingService.Api.Authorization;

/// <summary>
/// Guards service-to-service ("internal") endpoints. Requires the caller to present the shared
/// secret (config <c>InternalServices:ServiceKey</c>) in the <c>X-Internal-Key</c> header.
///
/// <para><c>X-Service-Key</c> is accepted as a <b>legacy</b> alternative. Two ticketing endpoints —
/// internal/work-update and internal/service-request-status — were built before this attribute
/// existed and check the key inline under that header name, with a plain <c>!=</c> string comparison.
/// A plain comparison short-circuits at the first differing byte and so leaks the secret through
/// timing, which is exactly what <see cref="FixedTimeEquals"/> exists to prevent; the same objection
/// is written into #359's partner-API design. Accepting both names lets those two endpoints move onto
/// this attribute without a coordinated deploy of the operations and fleet clients that call them.</para>
///
/// <para>The legacy name should go once those callers send <c>X-Internal-Key</c>. Until then
/// fleet-service's TicketingServiceClient has to send different headers to different ticketing
/// endpoints, and its own comment says so.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ServiceKeyAuthorizeAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-Internal-Key";

    /// <summary>Deprecated header used by internal/work-update and internal/service-request-status.</summary>
    public const string LegacyHeaderName = "X-Service-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = configuration["InternalServices:ServiceKey"];

        if (string.IsNullOrEmpty(expected))
        {
            context.Result = new ObjectResult(new { message = "Internal service key not configured." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
            return;
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].FirstOrDefault()
                       ?? context.HttpContext.Request.Headers[LegacyHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(provided) || !FixedTimeEquals(provided, expected))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid or missing internal service key." });
            return;
        }

        await next();
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
