namespace LicenseService.Core.DTOs.Common;

public class ApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int StatusCode { get; set; }
    public List<string>? Errors { get; set; }
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }
}
