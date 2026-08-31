using UserService.Core.DTOs.Modules;

namespace UserService.Core.Interfaces.Services;

public interface ISystemModuleService
{
    Task<IEnumerable<SystemModuleDto>> GetAllAsync();
    Task<SystemModuleDto> ToggleAsync(string moduleKey, bool enabled);
}
