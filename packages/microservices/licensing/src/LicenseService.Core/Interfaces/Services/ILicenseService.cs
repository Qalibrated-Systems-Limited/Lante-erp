using LicenseService.Core.DTOs.Licenses;

namespace LicenseService.Core.Interfaces.Services;

public interface ILicenseService
{
    Task<IssueLicenseResponseDto> IssueAsync(IssueLicenseDto dto, string issuedBy);
    Task<ValidateLicenseResponseDto> ValidateAsync(ValidateLicenseDto dto);
    Task<LicenseReadDto?> GetByIdAsync(string id);
    Task<IEnumerable<LicenseReadDto>> GetAllAsync(string? customerId, string? appId, bool? active);
    Task RevokeAsync(string id, string reason, string revokedBy);
    Task<LicenseReadDto> RenewAsync(string id, DateTime newExpiresAt, string renewedBy);
    Task<IEnumerable<LicenseReadDto>> GetExpiringAsync(int withinDays = 30);
}
