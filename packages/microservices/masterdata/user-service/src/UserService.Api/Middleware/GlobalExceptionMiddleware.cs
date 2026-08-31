using System.Net;
using System.Text.Json;
using UserService.Core.DTOs.Common;

namespace UserService.Api.Middleware;

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

        var response = new ApiResponse<object> { Success = false };

        switch (exception)
        {
            case KeyNotFoundException:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Message = "The requested resource was not found.";
                response.StatusCode = 404;
                break;
            case UnauthorizedAccessException:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Message = "You are not authorized to access this resource.";
                response.StatusCode = 401;
                break;
            case InvalidOperationException:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = exception.Message;
                response.StatusCode = 400;
                break;
            case ArgumentNullException:
            case ArgumentException:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = "Invalid request parameters.";
                response.StatusCode = 400;
                break;
            case Microsoft.EntityFrameworkCore.DbUpdateException dbEx
                when dbEx.InnerException is Npgsql.PostgresException pgEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = pgEx.SqlState switch
                {
                    "23505" => "A record with this value already exists.",
                    "23503" => "Referenced record does not exist.",
                    "23514" => "Data validation failed.",
                    _ => "A database error occurred. Kindly contact KMK for support."
                };
                response.StatusCode = 400;
                break;
            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Message = "An unexpected error occurred. Kindly contact KMK for support.";
                response.StatusCode = 500;
                break;
        }

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }
}
