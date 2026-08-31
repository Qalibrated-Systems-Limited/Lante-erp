using UserService.Core.DTOs.Settings;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;

namespace UserService.Core.Services;

public class SystemSettingService(ISystemSettingRepository repository) : ISystemSettingService
{
    public async Task<IEnumerable<SystemSettingDto>> GetAllAsync()
    {
        var settings = await repository.GetAllAsync();
        return settings.Select(s => new SystemSettingDto(s.Key, s.Value));
    }

    public async Task<SystemSettingDto> UpsertAsync(string key, string value)
    {
        var existing = await repository.GetByKeyAsync(key);
        if (existing == null)
        {
            var created = new SystemSetting { Key = key, Value = value };
            await repository.CreateAsync(created);
            return new SystemSettingDto(created.Key, created.Value);
        }

        existing.Value = value;
        await repository.UpdateAsync(existing);
        return new SystemSettingDto(existing.Key, existing.Value);
    }
}
