using AutoMapper;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UserService.Core.DTOs.Auth;
using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Users;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Emails;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;
using UserService.Core.Security;

namespace UserService.Core.Services;

public class UserService(
    IUserRepository userRepository,
    IDepartmentRepository departmentRepository,
    IMapper mapper,
    IRoleRepository roleRepository,
    IEmailQueueService emailQueueService,
    PasswordPolicyService passwordPolicyService,
    ITenantRepository tenantRepository,
    IUserDirectory userDirectory,
    IPublicRoleDirectorySync publicRoleDirectorySync,
    IConfiguration configuration,
    ILogger<UserService> logger)
    : IUserService
{
    private readonly PasswordPolicyService _passwordPolicyService = passwordPolicyService ?? throw new ArgumentNullException(nameof(passwordPolicyService));
    private async Task PopulateTenantContextAsync(UserReadDto dto)
    {
        var userTenant = await tenantRepository.GetDefaultTenantForUserAsync(dto.Id);
        if (userTenant == null) return;

        dto.TenantId = userTenant.TenantId;
        // BranchId + IsCompanyAdmin now come from the User itself (mapped in MappingProfile), having
        // been collapsed off UserTenant. Fallback to UserTenant only for a not-yet-migrated user.
        if (dto.BranchId == null && !dto.IsCompanyAdmin)
        {
            dto.BranchId = userTenant.BranchId;
            dto.IsCompanyAdmin = userTenant.BranchId == null;
        }
        dto.HqBranchId    = await tenantRepository.GetHqBranchIdForTenantAsync(userTenant.TenantId);
        var tenant = await tenantRepository.GetByIdAsync(userTenant.TenantId);
        dto.TenantName = tenant?.Name;
        // Route to the tenant's schema once it has been migrated (non-empty SchemaName). The token
        // only emits a `schema` claim when this is set, so tenants still resident in public (empty
        // SchemaName) keep hitting public + RLS. This is the runtime cutover switch per tenant.
        dto.SchemaName = tenant?.SchemaName;
        if (!string.IsNullOrEmpty(dto.BranchId))
        {
            var branch = await tenantRepository.GetBranchByIdAsync(dto.BranchId);
            dto.BranchName = branch?.Name;
        }
    }

    private async Task<List<string>> ResolveDepartmentIdsAsync(string? departmentId, Department? department)
    {
        if (string.IsNullOrEmpty(departmentId)) return [];
        if (!string.IsNullOrEmpty(department?.DepartmentGroupId))
            return await departmentRepository.GetDepartmentIdsByGroupAsync(department.DepartmentGroupId);
        return [departmentId];
    }

    public async Task<UserReadDto?> GetByIdAsync(string id)
    {
        var user = await userRepository.GetByIdAsync(id, true);
        if (user == null) return null;
        var dto = mapper.Map<UserReadDto>(user);
        dto.DepartmentIds = await ResolveDepartmentIdsAsync(user.DepartmentId, user.Department);
        await PopulateTenantContextAsync(dto);
        return dto;
    }

    public async Task<UserReadDto> CreateAsync(CreateUserDto dto)
    {
        // Single-login via invites: no password is set or emailed. The user's identity is written to
        // the control-plane directory (public.Users) so they can sign in at lante.africa
        // without a tenant subdomain, and a hashed, single-use, expiring token drives the invite link.
        var rawToken = InviteTokens.Generate();
        var userId = Guid.NewGuid().ToString();

        // A user row shared by both writes (same Id so the tenant record and the public login
        // pointer refer to the same person). DepartmentId/BranchId are set per-target below.
        User BuildUser() => new User
        {
            Id                   = userId,
            FirstName            = dto.FirstName,
            LastName             = dto.LastName,
            MobileNumber         = dto.MobileNumber,
            Email                = dto.Email,
            Password             = null,
            IsCompanyAdmin       = string.IsNullOrEmpty(dto.BranchId),
            IsFirstLogin         = true,
            IsActive             = false,
            InviteTokenHash      = InviteTokens.Hash(rawToken),
            InviteTokenExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt            = DateTime.UtcNow,
            UpdatedAt            = DateTime.UtcNow
        };

        var user = BuildUser();

        var tenant = await tenantRepository.GetByIdAsync(dto.TenantId ?? string.Empty);
        var hasSchema = !string.IsNullOrEmpty(tenant?.SchemaName) && tenant.SchemaName != "public";

        if (hasSchema)
        {
            // Source of truth = the tenant's own schema. The department + roles the admin picked are
            // valid here (the New-User form loads them from this schema), and the Users list reads
            // from here — so the new user shows up immediately. The request's search_path is already
            // the admin's tenant schema.
            user.DepartmentId = dto.DepartmentId;
            user.BranchId     = dto.BranchId;
            await userRepository.CreateAsync(user);
            if (dto.RoleIds != null)
                foreach (var roleId in dto.RoleIds)
                    await userRepository.AddUserRoleAsync(user.Id, roleId);

            // Thin login pointer in public (identity + tenant link, no tenant-scoped FKs) so the
            // invited user can sign in at lante.africa by email.
            await userDirectory.CreateDirectoryPointerAsync(BuildUser(), dto.RoleIds, dto.TenantId);
        }
        else
        {
            // Legacy tenant still resident in public — everything lives in public (dept/roles are
            // public IDs there), so keep the original single-write behaviour.
            user.DepartmentId = dto.DepartmentId;
            user.BranchId     = dto.BranchId;
            await userDirectory.CreateInvitedUserAsync(user, dto.RoleIds, dto.TenantId, dto.BranchId);
        }

        try
        {
            var baseUrl = (configuration["App:FrontendBaseUrl"] ?? "https://lante.africa").TrimEnd('/');
            var link = $"{baseUrl}/accept-invite?token={Uri.EscapeDataString(rawToken)}";
            var emailBody = $"""
                <h2>Welcome to Lante, {dto.FirstName}!</h2>
                <p>An account has been created for you. Click below to set your password and get started.</p>
                <p><a href="{link}" style="display:inline-block;padding:11px 22px;background:#1B3A5C;color:#fff;border-radius:8px;text-decoration:none;font-weight:700">Accept your invite</a></p>
                <p style="font-size:12px;color:#64748b">Or open this link:<br/>{link}</p>
                <p style="font-size:12px;color:#94A3B8">This invite expires in 7 days and can be used once.</p>
                <br/><p>Best regards,<br/>The Lante Team</p>
                """;
            await emailQueueService.EnqueueEmailAsync(dto.Email, "You're invited to Lante", emailBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send invite email to {Email}", dto.Email);
        }

        return mapper.Map<UserReadDto>(user);
    }

    public async Task<InviteInfoDto?> GetInviteAsync(string token)
    {
        var user = await userDirectory.FindByInviteTokenHashAsync(InviteTokens.Hash(token));
        if (user == null || user.InviteTokenExpiresAt == null || user.InviteTokenExpiresAt < DateTime.UtcNow)
            return null;

        string? companyName = null;
        var ut = await tenantRepository.GetDefaultTenantForUserAsync(user.Id);
        if (ut != null)
        {
            var tenant = await tenantRepository.GetByIdAsync(ut.TenantId);
            companyName = tenant?.Name;
        }
        return new InviteInfoDto { Email = user.Email, FirstName = user.FirstName, LastName = user.LastName, CompanyName = companyName };
    }

    public async Task<(bool ok, string? error)> AcceptInviteAsync(string token, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword) return (false, "Passwords do not match.");

        var tokenHash = InviteTokens.Hash(token);
        var user = await userDirectory.FindByInviteTokenHashAsync(tokenHash);
        if (user == null || user.InviteTokenExpiresAt == null || user.InviteTokenExpiresAt < DateTime.UtcNow)
            return (false, "This invite link is invalid or has expired. Please ask your administrator to resend it.");

        try
        {
            var policy = await _passwordPolicyService.GetCurrentPolicyAsync();
            if (policy != null) _passwordPolicyService.ValidatePassword(newPassword, policy);
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException ex)
        {
            return (false, ex.Message);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        var updated = await userDirectory.AcceptInviteAsync(tokenHash, passwordHash);
        return updated == null
            ? (false, "This invite link is invalid or has expired.")
            : (true, null);
    }

    public async Task<UserReadDto?> UpdateAsync(string id, UpdateUserDto dto)
    {
        var user = await userRepository.GetByIdAsync(id);
        if (user == null) return null;

        if (dto.FirstName != null) user.FirstName = dto.FirstName;
        if (dto.LastName != null) user.LastName = dto.LastName;
        if (dto.MobileNumber != null) user.MobileNumber = dto.MobileNumber;
        if (dto.DepartmentId != null) user.DepartmentId = dto.DepartmentId;

        var emailChanged = false;
        if (dto.Email != null && !dto.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await userRepository.GetByEmailAsync(dto.Email);
            if (existing != null && existing.Id != id)
                throw new InvalidOperationException("Another user already has this email address.");

            user.Email = dto.Email;
            emailChanged = true;
        }

        user.UpdatedAt = DateTime.UtcNow;

        await userRepository.UpdateAsync(user);

        // The Update endpoint writes whatever schema the request landed in (the tenant schema for a
        // schema-per-tenant user) — but single-login resolves users by email against the public
        // directory pointer, a separate row. Without this, the edit would look like it "took" in the
        // user list while login/invite-acceptance kept using the old address.
        if (emailChanged)
            await userDirectory.SyncUserAuthStateAsync(user);

        return mapper.Map<UserReadDto>(user);
    }

    public async Task<bool> ResendInviteAsync(string userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) throw new KeyNotFoundException($"User {userId} not found.");
        if (user.IsActive || !user.IsFirstLogin)
            throw new InvalidOperationException("This user has already accepted their invite.");

        var rawToken = InviteTokens.Generate();
        var tokenHash = InviteTokens.Hash(rawToken);
        var expiresAt = DateTime.UtcNow.AddDays(7);

        user.InviteTokenHash = tokenHash;
        user.InviteTokenExpiresAt = expiresAt;
        user.UpdatedAt = DateTime.UtcNow;
        await userRepository.UpdateAsync(user);

        // See UpdateAsync's equivalent call: GetInviteAsync/AcceptInviteAsync look the token hash up
        // in the public directory, not the tenant schema copy just updated above.
        await userDirectory.SyncUserAuthStateAsync(user);

        try
        {
            var baseUrl = (configuration["App:FrontendBaseUrl"] ?? "https://lante.africa").TrimEnd('/');
            var link = $"{baseUrl}/accept-invite?token={Uri.EscapeDataString(rawToken)}";
            var emailBody = $"""
                <h2>Welcome to Lante, {user.FirstName}!</h2>
                <p>Your invite has been resent. Click below to set your password and get started.</p>
                <p><a href="{link}" style="display:inline-block;padding:11px 22px;background:#1B3A5C;color:#fff;border-radius:8px;text-decoration:none;font-weight:700">Accept your invite</a></p>
                <p style="font-size:12px;color:#64748b">Or open this link:<br/>{link}</p>
                <p style="font-size:12px;color:#94A3B8">This invite expires in 7 days and can be used once.</p>
                <br/><p>Best regards,<br/>The Lante Team</p>
                """;
            await emailQueueService.EnqueueEmailAsync(user.Email, "You're invited to Lante", emailBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send resend-invite email to {Email}", user.Email);
        }

        return true;
    }

    public async Task<bool> DeleteAsync(string id) => await userRepository.DeleteAsync(id);
    public async Task<bool> RestoreAsync(string id) => await userRepository.RestoreAsync(id);

    public async Task<PaginatedResult<UserReadDto>> GetPagedAsync(PaginationParameters parameters)
    {
        var result = await userRepository.GetPagedAsync(parameters);
        return new PaginatedResult<UserReadDto>
        {
            Items = mapper.Map<IEnumerable<UserReadDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<PaginatedResult<UserReadDto>> GetDeletedPagedAsync(PaginationParameters parameters)
    {
        var result = await userRepository.GetDeletedPagedAsync(parameters);
        return new PaginatedResult<UserReadDto>
        {
            Items = mapper.Map<IEnumerable<UserReadDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<PaginatedResult<UserReadDto>> GetFilteredPagedAsync(UserFilterParameters parameters)
    {
        var result = await userRepository.GetFilteredPagedAsync(parameters);
        return new PaginatedResult<UserReadDto>
        {
            Items = mapper.Map<IEnumerable<UserReadDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<UserReadDto?> ValidateUserCredentials(string email, string password)
    {
        var user = await userRepository.GetByEmailAsync(email);
        if (user == null || user.IsDeleted) return null;
        if (string.IsNullOrEmpty(user.Password) || !BCrypt.Net.BCrypt.Verify(password, user.Password)) return null;
        var dto = mapper.Map<UserReadDto>(user);
        dto.DepartmentIds = await ResolveDepartmentIdsAsync(user.DepartmentId, user.Department);
        await PopulateTenantContextAsync(dto);
        return dto;
    }

    public async Task<UserReadDto?> UpdatePassword(string userId, UpdatePasswordDto dto,
                                                   bool identityAlreadyProven = false)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) throw new KeyNotFoundException($"User {userId} not found.");

        // First login (fresh invite, or after an admin reset) means the "current" password is just a
        // temp/placeholder value the user shouldn't need to know — but skipping the check is only safe
        // when something upstream has ACTUALLY proven who is calling.
        //
        // It previously skipped on `IsFirstLogin` alone. That field defaults to true, so every account was
        // exposed from creation until first sign-in: an anonymous caller with the user id could set a new
        // password and be handed a session (#263). `identityAlreadyProven` defaults to FALSE so a caller
        // that has not thought about it gets the safe behaviour, and a future second caller cannot inherit
        // the bypass by forgetting this argument exists.
        var skipCurrentPasswordCheck = user.IsFirstLogin && identityAlreadyProven;
        if (!skipCurrentPasswordCheck && !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.Password))
            throw new System.ComponentModel.DataAnnotations.ValidationException("Current password is incorrect.");

        var policy = await _passwordPolicyService.GetCurrentPolicyAsync();
        if (policy != null)
            _passwordPolicyService.ValidatePassword(dto.NewPassword, policy);

        user.Password = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
        user.IsFirstLogin = false;
        user.UpdatedAt = DateTime.UtcNow;

        await userRepository.UpdateAsync(user);

        // Whichever schema this request's connection landed on (public for a header-less call, the
        // tenant schema for one scoped into a company) is now current — the other copy is a separate
        // row that would otherwise keep the OLD password/first-login state forever, trapping the user
        // in an infinite "first login" redirect (or a stale password) on whichever login path reads
        // the copy that never got updated. Mirrors AcceptInviteAsync.
        await userDirectory.SyncUserAuthStateAsync(user);

        var result = mapper.Map<UserReadDto>(user);
        result.DepartmentIds = await ResolveDepartmentIdsAsync(user.DepartmentId, user.Department);
        await PopulateTenantContextAsync(result);
        return result;
    }

    public async Task<bool> UpdateUserActiveStatusAsync(string userId, bool isActive)
    {
        var result = await userRepository.UpdateActiveStatusAsync(userId, isActive);
        if (!result) return false;

        var user = await userRepository.GetByIdAsync(userId);
        if (user != null) await userDirectory.SyncUserAuthStateAsync(user);
        return true;
    }

    public async Task<bool> SetTwoFactorEnabledAsync(string userId, bool enabled)
    {
        var result = await userRepository.SetTwoFactorEnabledAsync(userId, enabled);
        if (!result) return false;

        var user = await userRepository.GetByIdAsync(userId);
        if (user != null) await userDirectory.SyncUserAuthStateAsync(user);
        return true;
    }

    public async Task<bool> ResetUserPasswordAsync(string userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) throw new KeyNotFoundException($"User {userId} not found.");

        const string tempPassword = "ResetMe123!";
        user.Password = BCrypt.Net.BCrypt.HashPassword(tempPassword, workFactor: 12);
        user.IsFirstLogin = true;
        user.UpdatedAt = DateTime.UtcNow;

        await userRepository.UpdateAsync(user);

        // See UpdatePassword's equivalent call: without this, whichever copy wasn't the ambient one for
        // this request (public if the admin was scoped into a tenant, or vice versa) keeps the OLD
        // password/IsFirstLogin state, so the temp password emailed below would never actually work
        // for whichever login path reads that stale copy.
        await userDirectory.SyncUserAuthStateAsync(user);

        try
        {
            var emailBody = $"""
                <h2>Password Reset</h2>
                <p>Your Lante password has been reset.</p>
                <p><strong>Temporary Password:</strong> {tempPassword}</p>
                <p>Please log in and change your password immediately.</p>
                """;
            await emailQueueService.EnqueueEmailAsync(user.Email, "Lante Password Reset", emailBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
        }

        return true;
    }

    public async Task<IEnumerable<Permission>> GetUserPermissionsAsync(string userId)
    {
        return await userRepository.GetUserPermissionsAsync(userId);
    }

    public async Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(string roleName)
    {
        var role = await roleRepository.GetByNameAsync(roleName);
        if (role == null) return Enumerable.Empty<Permission>();
        return await roleRepository.GetPermissionsForRoleAsync(role.Id);
    }

    public async Task<IEnumerable<Role>> GetUserRolesByUserIdAsync(string userId)
    {
        var userRoles = await userRepository.GetUserRolesAsync(userId);
        return userRoles.Select(ur => ur.Role).Where(r => r != null);
    }

    public async Task<bool> AssignRoleToUserAsync(string userId, string roleId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) throw new KeyNotFoundException($"User {userId} not found.");
        var result = await userRepository.AddUserRoleAsync(userId, roleId);
        await publicRoleDirectorySync.SyncRoleAssignedAsync(userId, roleId);
        return result;
    }

    public async Task<bool> RemoveRoleFromUserAsync(string userId, string roleId)
    {
        var result = await userRepository.RemoveUserRoleAsync(userId, roleId);
        await publicRoleDirectorySync.SyncRoleRemovedAsync(userId, roleId);
        return result;
    }

    public async Task<IEnumerable<NotificationTargetDto>> GetNotificationTargetsAsync(string? departmentId)
    {
        var targets = new List<NotificationTargetDto>();
        var seen = new HashSet<string>();

        // Department managers for the given department
        if (!string.IsNullOrWhiteSpace(departmentId))
        {
            var managers = await userRepository.GetDepartmentManagersAsync(departmentId);
            foreach (var m in managers)
            {
                if (seen.Add(m.Id))
                    targets.Add(new NotificationTargetDto
                    {
                        UserId = m.Id,
                        Name = $"{m.FirstName} {m.LastName}",
                        Email = m.Email,
                        Reason = "Department Manager"
                    });
            }
        }

        // Administrators
        var admins = await userRepository.GetUsersByRoleIdAsync("role-admin");
        foreach (var a in admins)
        {
            if (seen.Add(a.Id))
                targets.Add(new NotificationTargetDto
                {
                    UserId = a.Id,
                    Name = $"{a.FirstName} {a.LastName}",
                    Email = a.Email,
                    Reason = "Administrator"
                });
        }

        // Managing Director
        var mds = await userRepository.GetUsersByRoleIdAsync("role-md");
        foreach (var md in mds)
        {
            if (seen.Add(md.Id))
                targets.Add(new NotificationTargetDto
                {
                    UserId = md.Id,
                    Name = $"{md.FirstName} {md.LastName}",
                    Email = md.Email,
                    Reason = "Managing Director"
                });
        }

        return targets;
    }
    public async Task<UserReadDto?> GoogleLoginAsync(string idToken)
    {
        // 1. Verify the token with Google
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { configuration["Google:ClientId"] }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid Google token");
            return null;
        }

        // 2. Check if user already exists. Every other account on this platform is created
        // exclusively via an admin invite (see CreateAsync above) — Google login must not be a
        // second, ungated self-registration path. A first-time Google sign-in for an
        // already-invited user (no GoogleId set yet) is still allowed below; only a completely
        // unknown email is rejected.
        var user = await userRepository.GetByEmailAsync(payload.Email);
        if (user == null)
        {
            logger.LogWarning("Google login rejected — no invited account for {Email}", payload.Email);
            return null;
        }

        // 3. Existing user — update Google fields if not set
        if (string.IsNullOrEmpty(user.GoogleId))
        {
            user.GoogleId       = payload.Subject;
            user.ProfilePicture = payload.Picture;
            user.AuthProvider   = "google";
            user.UpdatedAt      = DateTime.UtcNow;
            await userRepository.UpdateAsync(user);
        }

        // 4. Return mapped DTO (same as normal login)
        var dto = mapper.Map<UserReadDto>(user);
        dto.DepartmentIds = await ResolveDepartmentIdsAsync(user.DepartmentId, user.Department);
        await PopulateTenantContextAsync(dto);
        return dto;
    }
}
