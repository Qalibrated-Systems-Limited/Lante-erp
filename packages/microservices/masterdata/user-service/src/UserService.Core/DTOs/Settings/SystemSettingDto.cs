namespace UserService.Core.DTOs.Settings;

public record SystemSettingDto(string Key, string Value);

public record UpdateSystemSettingDto(string Key, string Value);
