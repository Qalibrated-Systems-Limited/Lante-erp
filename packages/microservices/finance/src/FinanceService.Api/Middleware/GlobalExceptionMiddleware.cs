using System.Text.Json;
using FinanceService.Core.DTOs;

namespace FinanceService.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var status = ex switch
            {
                KeyNotFoundException => 404,
                InvalidOperationException => 400,
                UnauthorizedAccessException => 403,
                _ => 500,
            };
            if (status == 500) _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            var body = ApiResponse<object>.Fail(status == 500 ? "An unexpected error occurred." : ex.Message, status);
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOpts));
        }
    }
}
