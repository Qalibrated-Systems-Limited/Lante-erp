namespace UserService.Core.DTOs.Modules;

public record SystemModuleDto(string ModuleId, string DisplayName, bool IsCore, bool IsEnabled);
