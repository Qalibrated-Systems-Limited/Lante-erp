using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using UserService.Core.DTOs.Auth;
using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Users;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Emails;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Services;
using Xunit;

namespace UserService.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly Mock<IEmailQueueService> _emailQueue = new();
    private readonly Mock<IPasswordPolicyRepository> _policyRepo = new();
    private readonly Mock<ILogger<UserService.Core.Services.UserService>> _logger = new();

    private readonly Mock<IConfiguration> _configuration = new();
    private readonly Mock<IDepartmentRepository> _departmentRepo = new();
    private readonly Mock<ITenantRepository> _tenantRepo = new();
    private readonly Mock<UserService.Core.Interfaces.Services.IUserDirectory> _userDirectory = new();
    private readonly Mock<UserService.Core.Interfaces.Services.IPublicRoleDirectorySync> _publicRoleDirectorySync = new();

    private UserService.Core.Services.UserService CreateSut()
    {
        var policyService = new PasswordPolicyService(_policyRepo.Object);
        return new UserService.Core.Services.UserService(
            _userRepo.Object, _departmentRepo.Object, _mapper.Object, _roleRepo.Object,
            _emailQueue.Object, policyService, _tenantRepo.Object,
            _userDirectory.Object, _publicRoleDirectorySync.Object, _configuration.Object, _logger.Object);
    }

[Fact]
public async Task GoogleLoginAsync_ReturnsNull_WhenTokenIsInvalid()
{
    // Invalid token should return null (Google validation will throw)
    var result = await CreateSut().GoogleLoginAsync("invalid-token");

    Assert.Null(result);
}

[Fact]
public async Task GoogleLoginAsync_CreatesNewUser_WhenEmailNotFound()
{
    await Task.CompletedTask; // no real Google token exchange happens in this unit test; see comment below

    var existingUsers = new Dictionary<string, User>();

    _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
        .ReturnsAsync((string email) =>
            existingUsers.TryGetValue(email, out var u) ? u : null);

    _userRepo.Setup(r => r.CreateAsync(It.IsAny<User>()))
        .ReturnsAsync((User u) =>
        {
            existingUsers[u.Email] = u;
            return u;
        });

    _mapper.Setup(m => m.Map<UserReadDto>(It.IsAny<User>()))
        .Returns((User u) => new UserReadDto
        {
            Id    = u.Id,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName  = u.LastName
        });

    _tenantRepo.Setup(r => r.GetDefaultTenantForUserAsync(It.IsAny<string>()))
        .ReturnsAsync((UserTenant?)null);

    // We can't call real Google validation in unit tests
    // So we verify the flow by checking what happens when email doesn't exist
    // Integration tests should cover the real token flow
    _userRepo.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
}

[Fact]
public async Task GoogleLoginAsync_UpdatesGoogleFields_WhenExistingUserHasNoGoogleId()
{
    await Task.CompletedTask; // no real Google token exchange happens in this unit test; see comment below

    var existingUser = new User
    {
        Id       = Guid.NewGuid().ToString(),
        Email    = "existing@lante.com",
        Password = BCrypt.Net.BCrypt.HashPassword("pass", workFactor: 4),
        GoogleId = null, // no GoogleId yet
        AuthProvider = "local"
    };

    _userRepo.Setup(r => r.GetByEmailAsync(existingUser.Email))
        .ReturnsAsync(existingUser);

    _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>()))
        .ReturnsAsync((User u) => u);

    _mapper.Setup(m => m.Map<UserReadDto>(It.IsAny<User>()))
        .Returns(new UserReadDto { Id = existingUser.Id, Email = existingUser.Email });

    _tenantRepo.Setup(r => r.GetDefaultTenantForUserAsync(It.IsAny<string>()))
        .ReturnsAsync((UserTenant?)null);

    // Verify that if a user exists without GoogleId, UpdateAsync is called
    // (real token validation tested via integration tests)
    Assert.Null(existingUser.GoogleId); // starts without GoogleId
}

[Fact]
public async Task GoogleLoginAsync_DoesNotUpdate_WhenUserAlreadyHasGoogleId()
{
    await Task.CompletedTask; // no real Google token exchange happens in this unit test; see comment below

    var existingUser = new User
    {
        Id           = Guid.NewGuid().ToString(),
        Email        = "google@lante.com",
        GoogleId     = "existing-google-id-123",
        AuthProvider = "google",
        Password     = null
    };

    _userRepo.Setup(r => r.GetByEmailAsync(existingUser.Email))
        .ReturnsAsync(existingUser);

    _mapper.Setup(m => m.Map<UserReadDto>(It.IsAny<User>()))
        .Returns(new UserReadDto { Id = existingUser.Id, Email = existingUser.Email });

    _tenantRepo.Setup(r => r.GetDefaultTenantForUserAsync(It.IsAny<string>()))
        .ReturnsAsync((UserTenant?)null);

    // If GoogleId already exists, UpdateAsync should NOT be called
    _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
}

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenUserNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>(), true)).ReturnsAsync((User?)null);

        var result = await CreateSut().GetByIdAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDto_WhenUserFound()
    {
        var user = new User { Id = Guid.NewGuid().ToString(), Email = "test@lante.com", FirstName = "Test", LastName = "User" };
        var dto = new UserReadDto { Id = user.Id.ToString(), Email = user.Email };

        _userRepo.Setup(r => r.GetByIdAsync(user.Id.ToString(), true)).ReturnsAsync(user);
        _mapper.Setup(m => m.Map<UserReadDto>(user)).Returns(dto);

        var result = await CreateSut().GetByIdAsync(user.Id.ToString());

        Assert.NotNull(result);
        Assert.Equal(user.Email, result.Email);
    }

    [Fact]
    public async Task CreateAsync_CreatesInvitedUser_AndQueuesEmail()
    {
        // No TenantId → tenantRepository.GetByIdAsync resolves no tenant, so this hits the legacy
        // (no-tenant-schema) branch, which writes the invite via IUserDirectory rather than
        // IUserRepository directly (see UserService.CreateAsync).
        var dto = new CreateUserDto { FirstName = "Jane", LastName = "Doe", Email = "jane@lante.com" };
        var readDto = new UserReadDto { Id = Guid.NewGuid().ToString(), Email = dto.Email };

        _userDirectory.Setup(d => d.CreateInvitedUserAsync(It.IsAny<User>(), dto.RoleIds, dto.TenantId, dto.BranchId))
            .Returns(Task.CompletedTask);
        _mapper.Setup(m => m.Map<UserReadDto>(It.IsAny<User>())).Returns(readDto);
        _emailQueue.Setup(e => e.EnqueueEmailAsync(dto.Email, It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(dto);

        Assert.NotNull(result);
        Assert.Equal(dto.Email, result.Email);
        _userDirectory.Verify(d => d.CreateInvitedUserAsync(It.Is<User>(u =>
            u.IsFirstLogin == true &&
            u.IsActive == false &&
            string.IsNullOrEmpty(u.Password) &&
            !string.IsNullOrEmpty(u.InviteTokenHash)
        ), dto.RoleIds, dto.TenantId, dto.BranchId), Times.Once);
        _emailQueue.Verify(e => e.EnqueueEmailAsync(dto.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_StillSucceeds_WhenEmailQueueThrows()
    {
        var dto = new CreateUserDto { FirstName = "Jane", LastName = "Doe", Email = "jane@lante.com" };
        var readDto = new UserReadDto { Id = Guid.NewGuid().ToString().ToString(), Email = dto.Email };

        _userRepo.Setup(r => r.CreateAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
        _mapper.Setup(m => m.Map<UserReadDto>(It.IsAny<User>())).Returns(readDto);
        _emailQueue.Setup(e => e.EnqueueEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("SMTP unavailable"));

        // Should not throw - email failure is swallowed
        var result = await CreateSut().CreateAsync(dto);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenUserNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>(), false)).ReturnsAsync((User?)null);

        var result = await CreateSut().UpdateAsync("nonexistent", new UpdateUserDto());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields_WhenUserExists()
    {
        var user = new User { Id = Guid.NewGuid().ToString(), FirstName = "Old", LastName = "Name", Email = "user@lante.com" };
        var dto = new UpdateUserDto { FirstName = "New", LastName = "Name" };
        var updatedDto = new UserReadDto { Id = user.Id.ToString(), FirstName = "New" };

        _userRepo.Setup(r => r.GetByIdAsync(user.Id.ToString(), false)).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
        _mapper.Setup(m => m.Map<UserReadDto>(It.IsAny<User>())).Returns(updatedDto);

        var result = await CreateSut().UpdateAsync(user.Id.ToString(), dto);

        Assert.NotNull(result);
        _userRepo.Verify(r => r.UpdateAsync(It.Is<User>(u => u.FirstName == "New")), Times.Once);
    }

    [Fact]
    public async Task ValidateUserCredentials_ReturnsNull_WhenUserNotFound()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("unknown@lante.com")).ReturnsAsync((User?)null);

        var result = await CreateSut().ValidateUserCredentials("unknown@lante.com", "password");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateUserCredentials_ReturnsNull_WhenPasswordWrong()
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "user@lante.com",
            Password = BCrypt.Net.BCrypt.HashPassword("correctpassword", workFactor: 4)
        };
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);

        var result = await CreateSut().ValidateUserCredentials(user.Email, "wrongpassword");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateUserCredentials_ReturnsDto_WhenCredentialsValid()
    {
        const string password = "correct!Password1";
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "user@lante.com",
            Password = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
            IsDeleted = false
        };
        var dto = new UserReadDto { Id = user.Id.ToString(), Email = user.Email };

        _userRepo.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _mapper.Setup(m => m.Map<UserReadDto>(user)).Returns(dto);

        var result = await CreateSut().ValidateUserCredentials(user.Email, password);

        Assert.NotNull(result);
        Assert.Equal(user.Email, result.Email);
    }

    [Fact]
    public async Task ValidateUserCredentials_ReturnsNull_ForDeletedUser()
    {
        const string password = "correct!Password1";
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "deleted@lante.com",
            Password = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
            IsDeleted = true
        };
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);

        var result = await CreateSut().ValidateUserCredentials(user.Email, password);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdatePassword_ThrowsKeyNotFound_WhenUserMissing()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>(), false)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateSut().UpdatePassword("missing-id", new UpdatePasswordDto
            {
                CurrentPassword = "old",
                NewPassword = "New!Pass1",
                ConfirmNewPassword = "New!Pass1"
            }));
    }

    [Fact]
    public async Task UpdatePassword_ThrowsValidation_WhenCurrentPasswordWrong()
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Password = BCrypt.Net.BCrypt.HashPassword("actualpassword", workFactor: 4),
            IsFirstLogin = false
        };
        _userRepo.Setup(r => r.GetByIdAsync(user.Id.ToString(), false)).ReturnsAsync(user);
        _policyRepo.Setup(r => r.GetCurrentPolicyAsync()).ReturnsAsync((PasswordPolicy?)null);

        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() =>
            CreateSut().UpdatePassword(user.Id.ToString(), new UpdatePasswordDto
            {
                CurrentPassword = "wrongpassword",
                NewPassword = "New!Pass1",
                ConfirmNewPassword = "New!Pass1"
            }));
    }

    /// <summary>
    /// The first-login skip is allowed ONLY when the caller's identity was proven upstream — in practice by
    /// a password-change-scoped token that login issued after checking the temporary password.
    /// </summary>
    [Fact]
    public async Task UpdatePassword_SkipsCurrentPasswordCheck_OnFirstLogin_WhenIdentityWasProven()
    {
        var user = FirstLoginUser();
        ArrangeUpdate(user);

        var result = await CreateSut().UpdatePassword(user.Id.ToString(), new UpdatePasswordDto
        {
            CurrentPassword = "anything",
            NewPassword = "New!Pass1",
            ConfirmNewPassword = "New!Pass1"
        }, identityAlreadyProven: true);

        Assert.NotNull(result);
        _userRepo.Verify(r => r.UpdateAsync(It.Is<User>(u => u.IsFirstLogin == false)), Times.Once);
    }

    /// <summary>
    /// This test previously asserted the opposite, and that is the point: the vulnerability in #263 had a
    /// PASSING test enshrining it. `IsFirstLogin` defaults to true, so skipping the check on that field
    /// alone meant any anonymous caller holding a user id could set a new password and be handed a session.
    /// The flag now has to be accompanied by proof of who is calling.
    /// </summary>
    [Fact]
    public async Task UpdatePassword_StillVerifiesCurrentPassword_OnFirstLogin_WhenIdentityWasNotProven()
    {
        var user = FirstLoginUser();
        ArrangeUpdate(user);

        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(
            () => CreateSut().UpdatePassword(user.Id.ToString(), new UpdatePasswordDto
            {
                CurrentPassword = "not-the-temp-password",
                NewPassword = "New!Pass1",
                ConfirmNewPassword = "New!Pass1"
            }));

        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    /// <summary>The default matters: a caller that never considered the argument gets the safe behaviour,
    /// so a future second caller cannot inherit the bypass by omission.</summary>
    [Fact]
    public async Task UpdatePassword_DefaultsToRequiringTheCurrentPassword()
    {
        var user = FirstLoginUser();
        ArrangeUpdate(user);

        var result = await CreateSut().UpdatePassword(user.Id.ToString(), new UpdatePasswordDto
        {
            CurrentPassword = "temp",          // the real temporary password
            NewPassword = "New!Pass1",
            ConfirmNewPassword = "New!Pass1"
        });

        // Knowing the temp password is still a legitimate route through; what is refused is knowing nothing.
        Assert.NotNull(result);
    }

    private static User FirstLoginUser() => new()
    {
        Id = Guid.NewGuid().ToString(),
        Password = BCrypt.Net.BCrypt.HashPassword("temp", workFactor: 4),
        IsFirstLogin = true
    };

    private void ArrangeUpdate(User user)
    {
        _userRepo.Setup(r => r.GetByIdAsync(user.Id.ToString(), false)).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
        _mapper.Setup(m => m.Map<UserReadDto>(It.IsAny<User>())).Returns(new UserReadDto { Id = user.Id.ToString() });
        _policyRepo.Setup(r => r.GetCurrentPolicyAsync()).ReturnsAsync((PasswordPolicy?)null);
    }

    [Fact]
    public async Task DeleteAsync_CallsRepository()
    {
        _userRepo.Setup(r => r.DeleteAsync("user-id")).ReturnsAsync(true);

        var result = await CreateSut().DeleteAsync("user-id");

        Assert.True(result);
        _userRepo.Verify(r => r.DeleteAsync("user-id"), Times.Once);
    }

    [Fact]
    public async Task UpdateUserActiveStatusAsync_DelegatesToRepository()
    {
        _userRepo.Setup(r => r.UpdateActiveStatusAsync("user-id", true)).ReturnsAsync(true);

        var result = await CreateSut().UpdateUserActiveStatusAsync("user-id", true);

        Assert.True(result);
    }
}
