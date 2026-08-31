using Microsoft.Extensions.Logging;
using UserService.Core.DTOs.Auth;
using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Users;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Services;

namespace UserService.Core.Services;

public class CachedUserService(
    UserService inner,
    ICacheService cacheService)
    : IUserService
{
    private static string UserCacheKey(string id) => $"user:{id}";

    public async Task<UserReadDto?> GetByIdAsync(string id)
    {
        var cached = await cacheService.GetAsync<UserReadDto>(UserCacheKey(id));
        if (cached != null) return cached;

        var user = await inner.GetByIdAsync(id);
        if (user != null)
            await cacheService.SetAsync(UserCacheKey(id), user, TimeSpan.FromMinutes(15));

        return user;
    }

    public async Task<UserReadDto> CreateAsync(CreateUserDto dto)
    {
        var user = await inner.CreateAsync(dto);
        await cacheService.SetAsync(UserCacheKey(user.Id), user, TimeSpan.FromMinutes(15));
        return user;
    }

    public Task<InviteInfoDto?> GetInviteAsync(string token) => inner.GetInviteAsync(token);

    public Task<(bool ok, string? error)> AcceptInviteAsync(string token, string newPassword, string confirmPassword)
        => inner.AcceptInviteAsync(token, newPassword, confirmPassword);

    public async Task<UserReadDto?> UpdateAsync(string id, UpdateUserDto dto)
    {
        var user = await inner.UpdateAsync(id, dto);
        if (user != null)
            await cacheService.SetAsync(UserCacheKey(id), user, TimeSpan.FromMinutes(15));
        return user;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await cacheService.RemoveAsync(UserCacheKey(id));
        return await inner.DeleteAsync(id);
    }

    public async Task<bool> RestoreAsync(string id) => await inner.RestoreAsync(id);

    public Task<PaginatedResult<UserReadDto>> GetPagedAsync(PaginationParameters parameters)
        => inner.GetPagedAsync(parameters);

    public Task<PaginatedResult<UserReadDto>> GetDeletedPagedAsync(PaginationParameters parameters)
        => inner.GetDeletedPagedAsync(parameters);

    public Task<PaginatedResult<UserReadDto>> GetFilteredPagedAsync(UserFilterParameters parameters)
        => inner.GetFilteredPagedAsync(parameters);

    public Task<UserReadDto?> ValidateUserCredentials(string email, string password)
        => inner.ValidateUserCredentials(email, password);

    public async Task<UserReadDto?> UpdatePassword(string userId, UpdatePasswordDto dto,
                                                   bool identityAlreadyProven = false)
    {
        var user = await inner.UpdatePassword(userId, dto, identityAlreadyProven);
        if (user != null)
            await cacheService.SetAsync(UserCacheKey(userId), user, TimeSpan.FromMinutes(15));
        return user;
    }

    public async Task<bool> UpdateUserActiveStatusAsync(string userId, bool isActive)
    {
        await cacheService.RemoveAsync(UserCacheKey(userId));
        return await inner.UpdateUserActiveStatusAsync(userId, isActive);
    }

    public async Task<bool> SetTwoFactorEnabledAsync(string userId, bool enabled)
    {
        await cacheService.RemoveAsync(UserCacheKey(userId));
        return await inner.SetTwoFactorEnabledAsync(userId, enabled);
    }

    public Task<bool> ResetUserPasswordAsync(string userId) => inner.ResetUserPasswordAsync(userId);
    public Task<bool> ResendInviteAsync(string userId) => inner.ResendInviteAsync(userId);
    public Task<IEnumerable<Permission>> GetUserPermissionsAsync(string userId) => inner.GetUserPermissionsAsync(userId);
    public Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(string roleName) => inner.GetPermissionsForRoleAsync(roleName);
    public Task<IEnumerable<Role>> GetUserRolesByUserIdAsync(string userId) => inner.GetUserRolesByUserIdAsync(userId);

    public async Task<bool> AssignRoleToUserAsync(string userId, string roleId)
    {
        await cacheService.RemoveAsync(UserCacheKey(userId));
        return await inner.AssignRoleToUserAsync(userId, roleId);
    }

    public async Task<bool> RemoveRoleFromUserAsync(string userId, string roleId)
    {
        await cacheService.RemoveAsync(UserCacheKey(userId));
        return await inner.RemoveRoleFromUserAsync(userId, roleId);
    }

    public Task<IEnumerable<NotificationTargetDto>> GetNotificationTargetsAsync(string? departmentId)
        => inner.GetNotificationTargetsAsync(departmentId);
    public Task<UserReadDto?> GoogleLoginAsync(string idToken)
        => inner.GoogleLoginAsync(idToken);
}
