using AutoMapper;
using Microsoft.Extensions.Logging;
using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Permissions;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;

namespace UserService.Core.Services;

public class PermissionsService(
    IPermissionsRepository permissionsRepository,
    IMapper mapper)
    : IPermissionsService
{
    public async Task<PaginatedResult<PermissionReadDto>> GetPagedAsync(PaginationParameters parameters)
    {
        var result = await permissionsRepository.GetPagedAsync(parameters);
        return new PaginatedResult<PermissionReadDto>
        {
            Items = mapper.Map<IEnumerable<PermissionReadDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<PermissionReadDto?> GetByIdAsync(string id)
    {
        var p = await permissionsRepository.GetByIdAsync(id);
        return p == null ? null : mapper.Map<PermissionReadDto>(p);
    }

    public async Task<PermissionReadDto> CreateAsync(CreatePermissionDto dto)
    {
        var existing = await permissionsRepository.GetByNameAsync(dto.Name);
        if (existing != null)
            throw new InvalidOperationException($"Permission '{dto.Name}' already exists.");

        var permission = new Permission
        {
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await permissionsRepository.CreateAsync(permission);
        return mapper.Map<PermissionReadDto>(permission);
    }

    public async Task<bool> DeleteAsync(string id) => await permissionsRepository.DeleteAsync(id);
}
