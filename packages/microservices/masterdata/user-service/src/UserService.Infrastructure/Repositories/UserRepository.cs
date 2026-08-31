using Microsoft.EntityFrameworkCore;
using UserService.Core.DTOs.Common;
using UserService.Core.Constants;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class UserRepository(LanteUserServiceDbContext context)
    : GenericRepository<User>(context), IUserRepository
{
    public new async Task<User?> GetByIdAsync(string id, bool includeDeleted = false)
    {
        var query = Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Include(u => u.UserPermissions)
                .ThenInclude(up => up.Permission)
            .Include(u => u.Department)
            .AsQueryable();

        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        return await query.FirstOrDefaultAsync(u => u.Id == id && (includeDeleted || !u.IsDeleted));
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Include(u => u.UserPermissions)
                .ThenInclude(up => up.Permission)
            .Include(u => u.Department)
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
    }

    public async Task<IEnumerable<Permission>> GetUserPermissionsAsync(string userId)
    {
        var rolePerms = await Context.UserRoles
            .Where(ur => ur.UserId == userId && !ur.IsDeleted && !ur.Role.IsDeleted)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Where(rp => !rp.IsDeleted)
            .Select(rp => rp.Permission)
            .Where(p => p != null && !p.IsDeleted)
            .Distinct()
            .ToListAsync();

        var directPerms = await Context.UserPermissions
            .Where(up => up.UserId == userId && !up.IsDeleted && !up.Permission.IsDeleted)
            .Select(up => up.Permission)
            .ToListAsync();

        return rolePerms.Union(directPerms).DistinctBy(p => p.Id).ToList();
    }

    // Reverse of GetUserPermissionsAsync above — every active user who holds this permission,
    // whether granted via a role or directly. Used to fan a notification out to e.g. everyone
    // holding fleet.write, without hardcoding a specific user.
    public async Task<IEnumerable<User>> GetUsersByPermissionAsync(string permissionCode)
    {
        // IsActive tracks whether the user is currently logged in, not whether the account is
        // enabled — do NOT filter on it here. A notification must still reach someone who holds
        // the permission but isn't logged in right now; that's the entire point of notifying them.
        var viaRole = await Context.Users
            .Where(u => !u.IsDeleted && u.UserRoles.Any(ur =>
                !ur.IsDeleted && !ur.Role.IsDeleted &&
                ur.Role.RolePermissions.Any(rp => !rp.IsDeleted && !rp.Permission.IsDeleted && rp.Permission.Name == permissionCode)))
            .ToListAsync();

        var viaDirect = await Context.Users
            .Where(u => !u.IsDeleted && u.UserPermissions.Any(up =>
                !up.IsDeleted && !up.Permission.IsDeleted && up.Permission.Name == permissionCode))
            .ToListAsync();

        // The Admin role is seeded with every permission by design (system-wide oversight), so a
        // literal "who holds this permission" match always sweeps Admin in — but Admin isn't the
        // intended audience for routine operational notifications from individual modules (fleet
        // requests, etc.). Exclude it here rather than in every caller.
        var adminIds = await Context.UserRoles
            .Where(ur => !ur.IsDeleted && !ur.Role.IsDeleted && ur.Role.Name == "Admin")
            .Select(ur => ur.UserId)
            .ToListAsync();

        return viaRole.Union(viaDirect).DistinctBy(u => u.Id).Where(u => !adminIds.Contains(u.Id)).ToList();
    }

    public async Task<IEnumerable<UserRole>> GetUserRolesAsync(string userId)
    {
        return await Context.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId && !ur.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetUsersByRoleAsync(string roleId)
    {
        return await Context.UserRoles
            .Where(ur => ur.RoleId == roleId && !ur.IsDeleted)
            .Select(ur => ur.User)
            .Where(u => u != null && !u.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetUsersByDepartmentAsync(string departmentId)
    {
        return await Context.Users
            .Where(u => u.DepartmentId == departmentId && !u.IsDeleted)
            .ToListAsync();
    }

    public async Task<PaginatedResult<User>> GetDeletedPagedAsync(PaginationParameters parameters)
    {
        var query = Context.Users
            .IgnoreQueryFilters()
            .Where(u => u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(search) ||
                u.LastName.ToLower().Contains(search) ||
                u.Email.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<User>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }

    public new async Task<PaginatedResult<User>> GetPagedAsync(PaginationParameters parameters)
    {
        var query = Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.Department)
            .Include(u => u.UserTenants)
                .ThenInclude(ut => ut.Branch)
            // Also FAIL-OPEN: if this name stops matching, platform operators appear in ordinary tenant
            // user lists, which is exactly how their ids would leak (see #263 — ids are not enumerable,
            // but they are not secret either).
            .Where(u => !u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role != null && ur.Role.Name == WellKnownRoles.PlatformAdmin))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(search) ||
                u.LastName.ToLower().Contains(search) ||
                u.Email.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.FirstName)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<User>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }

    public async Task<PaginatedResult<User>> GetFilteredPagedAsync(UserFilterParameters parameters)
    {
        var query = Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(search) ||
                u.LastName.ToLower().Contains(search) ||
                u.Email.ToLower().Contains(search));
        }

        if (parameters.DepartmentIds is { Count: > 0 })
            query = query.Where(u => u.DepartmentId != null && parameters.DepartmentIds.Contains(u.DepartmentId));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.FirstName)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<User>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }

    public async Task<bool> UpdateActiveStatusAsync(string userId, bool isActive)
    {
        var user = await Context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return false;
        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetTwoFactorEnabledAsync(string userId, bool enabled)
    {
        var user = await Context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return false;
        user.TwoFactorEnabled = enabled;
        user.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddUserRoleAsync(string userId, string roleId)
    {
        var exists = await Context.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId && !ur.IsDeleted);
        if (exists) return true;

        await Context.UserRoles.AddAsync(new UserRole
        {
            Id        = Guid.NewGuid().ToString(),
            UserId    = userId,
            RoleId    = roleId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveUserRoleAsync(string userId, string roleId)
    {
        var ur = await Context.UserRoles
            .FirstOrDefaultAsync(r => r.UserId == userId && r.RoleId == roleId && !r.IsDeleted);
        if (ur == null) return false;
        Context.UserRoles.Remove(ur);
        await Context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<User>> GetDepartmentManagersAsync(string departmentId)
    {
        // Same IsActive fix as GetUsersByRoleIdAsync/GetUsersByPermissionAsync — it means
        // "currently logged in", not "account enabled", so it must not gate who gets notified.
        return await Context.UserRoles
            .Where(ur => !ur.IsDeleted && !ur.Role.IsDeleted &&
                         ur.User.DepartmentId == departmentId && !ur.User.IsDeleted &&
                         ur.Role.Name.Contains("Manager"))
            .Include(ur => ur.User)
            .Select(ur => ur.User)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetUsersByRoleIdAsync(string roleId)
    {
        // IsActive tracks whether the user is currently logged in, not whether the account is
        // enabled — don't filter on it here, same reasoning as GetUsersByPermissionAsync below.
        // This was previously excluding anyone in the role who wasn't logged in at the moment a
        // notification fired (e.g. department-assignment emails to admins/managers).
        return await Context.UserRoles
            .Where(ur => ur.RoleId == roleId && !ur.IsDeleted && !ur.User.IsDeleted)
            .Include(ur => ur.User)
            .Select(ur => ur.User)
            .Distinct()
            .ToListAsync();
    }

    // Role IDs aren't a stable cross-service constant (unlike "role-admin"), so cross-service
    // callers that know a role by its display name (e.g. fleet-service wanting "Fleet Manager"
    // specifically, not everyone who happens to hold fleet.write) go through this instead.
    public async Task<IEnumerable<User>> GetUsersByRoleNameAsync(string roleName)
    {
        return await Context.UserRoles
            .Where(ur => !ur.IsDeleted && !ur.Role.IsDeleted && ur.Role.Name == roleName && !ur.User.IsDeleted)
            .Include(ur => ur.User)
            .Select(ur => ur.User)
            .Distinct()
            .ToListAsync();
    }
}
