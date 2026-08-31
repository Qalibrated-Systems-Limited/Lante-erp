namespace UserService.Core.DTOs.Common;

public class UserFilterParameters : PaginationParameters
{
    public List<string>? DepartmentIds { get; set; }
}
