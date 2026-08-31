using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace UserService.Api.Authorization;

/// <summary>
/// Guards service-to-service ("internal") endpoints. Requires the caller to present the shared
/// secret (config <c>InternalServices:ServiceKey</c>) in the <c>X-Internal-Key</c> header.
/// This is the app-layer half of defence-in-depth; a network policy restricting these routes to
/// in-cluster callers is the other half (DevOps).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ServiceKeyAuthorizeAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-Internal-Key";

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

        var provided = context.HttpContext.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(provided) || !FixedTimeEquals(provided, expected))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid or missing internal service key." });
            return;
        }

        await next();
    }

    // Constant-time comparison to avoid leaking the key via timing.
    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
