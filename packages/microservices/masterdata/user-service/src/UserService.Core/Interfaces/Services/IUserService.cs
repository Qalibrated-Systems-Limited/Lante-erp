using UserService.Core.DTOs.Auth;
using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Users;
using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Services;

public interface IUserService
{
    Task<UserReadDto?> GetByIdAsync(string id);
    Task<UserReadDto> CreateAsync(CreateUserDto dto);
    Task<UserService.Core.DTOs.Auth.InviteInfoDto?> GetInviteAsync(string token);
    Task<(bool ok, string? error)> AcceptInviteAsync(string token, string newPassword, string confirmPassword);
    Task<UserReadDto?> UpdateAsync(string id, UpdateUserDto dto);
    Task<bool> DeleteAsync(string id);
    Task<bool> RestoreAsync(string id);
    Task<PaginatedResult<UserReadDto>> GetPagedAsync(PaginationParameters parameters);
    Task<PaginatedResult<UserReadDto>> GetFilteredPagedAsync(UserFilterParameters parameters);
    Task<PaginatedResult<UserReadDto>> GetDeletedPagedAsync(PaginationParameters parameters);
    Task<UserReadDto?> ValidateUserCredentials(string email, string password);
    /// <summary><paramref name="identityAlreadyProven"/> must only be true when the caller has verified
    /// the requester another way — a password-change-scoped token, not merely IsFirstLogin. See #263.</summary>
    Task<UserReadDto?> UpdatePassword(string userId, UpdatePasswordDto dto, bool identityAlreadyProven = false);
    Task<bool> UpdateUserActiveStatusAsync(string userId, bool isActive);
    Task<bool> SetTwoFactorEnabledAsync(string userId, bool enabled);
    Task<bool> ResetUserPasswordAsync(string userId);
    Task<bool> ResendInviteAsync(string userId);
    Task<IEnumerable<Permission>> GetUserPermissionsAsync(string userId);
    Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(string roleName);
    Task<IEnumerable<Role>> GetUserRolesByUserIdAsync(string userId);
    Task<bool> AssignRoleToUserAsync(string userId, string roleId);
    Task<bool> RemoveRoleFromUserAsync(string userId, string roleId);
    Task<IEnumerable<NotificationTargetDto>> GetNotificationTargetsAsync(string? departmentId);
    Task<UserReadDto?> GoogleLoginAsync(string idToken);
}
