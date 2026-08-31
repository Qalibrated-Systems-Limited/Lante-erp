namespace OperationsService.Core.DTOs.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public int StatusCode { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, StatusCode = 200, Message = message };

    public static ApiResponse<T> Created(T data, string? message = null) =>
        new() { Success = true, Data = data, StatusCode = 201, Message = message };

    public static ApiResponse<T> Fail(string message, int statusCode = 400) =>
        new() { Success = false, Message = message, StatusCode = statusCode };
}
