using AutoMapper;
using Moq;
using UserService.Core.Constants;
using UserService.Core.DTOs.Roles;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Services;
using Xunit;

namespace UserService.Tests.Services;

/// <summary>
/// The platform-operator role must not be renameable, deletable or deactivatable.
///
/// <para>Authorization in this service compares role <b>display names</b> — <c>user.Roles</c> holds names,
/// and <c>[Authorize(Roles = "Platform Admin")]</c> matches the role claim, which is a name. That makes the
/// name a security-critical value. Two of the comparisons fail OPEN if it stops matching: the tenant login
/// portal stops barring platform operators, and they stop being filtered out of tenant user lists.</para>
///
/// <para><c>RoleService</c> already refused to modify a role with <c>IsSystem</c> set. The defect was that
/// <b>nothing ever set it</b> — the flag had its own migration and then no writer — so the guard could
/// never fire and the role was freely editable. These tests pin both halves: the guard, and the flag
/// actually being on.</para>
/// </summary>
public class SystemRoleProtectionTests
{
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IPermissionsRepository> _permissions = new();
    private readonly Mock<IRolePermissionRepository> _rolePermissions = new();
    private readonly Mock<IMapper> _mapper = new();

    private RoleService Sut() => new(_roles.Object, _permissions.Object, _rolePermissions.Object, _mapper.Object);

    private Role ArrangeRole(bool isSystem) 
    {
        var role = new Role
        {
            Id = "role-platform-admin",
            Name = WellKnownRoles.PlatformAdmin,
            IsSystem = isSystem,
            IsActive = true,
        };
        _roles.Setup(r => r.GetByIdAsync(role.Id, false)).ReturnsAsync(role);
        return role;
    }

    [Fact]
    public async Task The_platform_admin_role_cannot_be_renamed()
    {
        var role = ArrangeRole(isSystem: true);

        var act = () => Sut().UpdateAsync(role.Id, new UpdateRoleDto { Name = "Platform Administrator" });

        // A rename is not cosmetic. It silently stops the tenant login portal barring platform operators.
        await Assert.ThrowsAsync<InvalidOperationException>(act);
        _roles.Verify(r => r.UpdateAsync(It.IsAny<Role>()), Times.Never);
    }

    [Fact]
    public async Task The_platform_admin_role_cannot_be_deleted()
    {
        var role = ArrangeRole(isSystem: true);

        var act = () => Sut().DeleteAsync(role.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(act);
        _roles.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task The_platform_admin_role_cannot_be_deactivated()
    {
        var role = ArrangeRole(isSystem: true);

        var act = () => Sut().UpdateAsync(role.Id, new UpdateRoleDto { IsActive = false });

        // An inactive role stops matching the checks that depend on it just as thoroughly as a renamed one.
        // Covered by the same single guard rather than one of its own: I briefly added a dedicated
        // `IsSystem && IsActive == false` check, and mutation testing showed deleting it changed nothing,
        // because the guard above throws first. An unreachable guard that reads as protection is exactly
        // the defect this file exists to prevent, so it went rather than staying as reassurance.
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task An_ordinary_role_is_still_editable()
    {
        var role = new Role { Id = "role-md", Name = "MD", IsSystem = false, IsActive = true };
        _roles.Setup(r => r.GetByIdAsync(role.Id, false)).ReturnsAsync(role);
        _roles.Setup(r => r.UpdateAsync(It.IsAny<Role>())).ReturnsAsync((Role r) => r);
        _mapper.Setup(m => m.Map<RoleReadDto>(It.IsAny<Role>())).Returns(new RoleReadDto());

        var result = await Sut().UpdateAsync(role.Id, new UpdateRoleDto { Description = "Managing Director" });

        // The lock has to be narrow. MD is documented as deliberately editable so its permissions can be
        // trimmed back through the UI; locking every seeded role would break that.
        Assert.NotNull(result);
    }

    [Fact]
    public async Task The_guard_does_nothing_when_the_flag_was_never_set()
    {
        var role = ArrangeRole(isSystem: false);
        _roles.Setup(r => r.UpdateAsync(It.IsAny<Role>())).ReturnsAsync((Role r) => r);
        _mapper.Setup(m => m.Map<RoleReadDto>(It.IsAny<Role>())).Returns(new RoleReadDto());

        await Sut().UpdateAsync(role.Id, new UpdateRoleDto { Name = "Renamed" });

        // This is the state every existing database was in, and it is why the guard alone was not enough:
        // the same role, the same code path, renamed without complaint because IsSystem was false. The
        // seeder is what has to put the flag on — see the backfill in DatabaseSeeder.
        Assert.Equal("Renamed", role.Name);
    }

    [Fact]
    public void The_locked_role_id_list_covers_the_platform_operator()
    {
        // The seeder keys the lock off ids, while authorization compares names. If these two ever describe
        // different roles the protection silently stops covering the thing it exists to protect.
        Assert.Contains("role-platform-admin", WellKnownRoles.LockedRoleIds);
    }
}
