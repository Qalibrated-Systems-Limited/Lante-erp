namespace ReportingService.Core.DTOs;

/// <summary>
/// Shared response envelope — matches the Success/Message/Data/Errors/StatusCode shape used by
/// FinanceService, OperationsService, StoreService, HSEService and ComplianceService. FleetService
/// returns a looser anonymous { success, data } shape without Errors/StatusCode, which still
/// deserializes cleanly into this same type (missing fields just default).
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public int StatusCode { get; set; }
}

/// <summary>
/// Generic paginated-list envelope matching OperationsService's PaginatedResult&lt;T&gt; and
/// StoreService's PaginatedResult&lt;T&gt; (both use Items/TotalCount/Page/PageSize/TotalPages).
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// FleetService's paged envelope uses PageNumber instead of Page — kept as a distinct type to
/// avoid a naming mismatch when deserializing FleetService.Api's Trips list response.
/// </summary>
public class FleetPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
