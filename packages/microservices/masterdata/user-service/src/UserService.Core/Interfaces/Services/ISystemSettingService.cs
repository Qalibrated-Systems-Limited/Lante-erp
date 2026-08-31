using UserService.Core.DTOs.Settings;

namespace UserService.Core.Interfaces.Services;

public interface ISystemSettingService
{
    Task<IEnumerable<SystemSettingDto>> GetAllAsync();
    Task<SystemSettingDto> UpsertAsync(string key, string value);
}
