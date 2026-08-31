using System.Net;
using System.Text.Json;

namespace LicenseService.Api.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        int statusCode;
        string message;

        switch (exception)
        {
            case KeyNotFoundException:
                statusCode = (int)HttpStatusCode.NotFound;
                message = "The requested resource was not found.";
                break;
            case UnauthorizedAccessException:
                statusCode = (int)HttpStatusCode.Unauthorized;
                message = "You are not authorized to access this resource.";
                break;
            case InvalidOperationException:
                statusCode = (int)HttpStatusCode.BadRequest;
                message = exception.Message;
                break;
            case ArgumentNullException:
            case ArgumentException:
                statusCode = (int)HttpStatusCode.BadRequest;
                message = "Invalid request parameters.";
                break;
            case Microsoft.EntityFrameworkCore.DbUpdateException dbEx
                when dbEx.InnerException is Npgsql.PostgresException pgEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                message = pgEx.SqlState switch
                {
                    "23505" => "A record with this value already exists.",
                    "23503" => "Referenced record does not exist.",
                    "23514" => "Data validation failed.",
                    _ => "A database error occurred. Kindly contact KMK for support."
                };
                break;
            default:
                statusCode = (int)HttpStatusCode.InternalServerError;
                message = "An unexpected error occurred. Kindly contact KMK for support.";
                break;
        }

        context.Response.StatusCode = statusCode;
        var json = JsonSerializer.Serialize(
            new { success = false, message, statusCode },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }
}
