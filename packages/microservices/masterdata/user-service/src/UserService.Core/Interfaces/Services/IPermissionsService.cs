using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Permissions;

namespace UserService.Core.Interfaces.Services;

public interface IPermissionsService
{
    Task<PaginatedResult<PermissionReadDto>> GetPagedAsync(PaginationParameters parameters);
    Task<PermissionReadDto?> GetByIdAsync(string id);
    Task<PermissionReadDto> CreateAsync(CreatePermissionDto dto);
    Task<bool> DeleteAsync(string id);
}
