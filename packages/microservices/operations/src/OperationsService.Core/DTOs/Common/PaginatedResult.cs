namespace OperationsService.Core.DTOs.Common;

public class PaginatedResult<T>
{
    public IEnumerable<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class PaginationParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public string? Search { get; set; }
    public bool SortDescending { get; set; } = true;
}
