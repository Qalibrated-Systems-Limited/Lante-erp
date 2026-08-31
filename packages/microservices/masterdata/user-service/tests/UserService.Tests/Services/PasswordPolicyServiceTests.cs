using Moq;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Services;
using Xunit;

namespace UserService.Tests.Services;

public class PasswordPolicyServiceTests
{
    private readonly Mock<IPasswordPolicyRepository> _repo = new();

    private PasswordPolicyService CreateSut() => new(_repo.Object);

    private static PasswordPolicy StrictPolicy() => new()
    {
        MinimumLength = 8,
        RequireUppercase = true,
        RequireLowercase = true,
        RequireDigit = true,
        RequireSpecialCharacter = true,
        MaxAgeDays = 90
    };

    [Fact]
    public void ValidatePassword_Passes_ForCompliantPassword()
    {
        var policy = StrictPolicy();
        // Should not throw
        CreateSut().ValidatePassword("Secure!1pass", policy);
    }

    [Fact]
    public void ValidatePassword_Throws_WhenTooShort()
    {
        var policy = StrictPolicy();
        var ex = Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() =>
            CreateSut().ValidatePassword("Ab!1", policy));
        Assert.Contains("8", ex.Message);
    }

    [Fact]
    public void ValidatePassword_Throws_WhenNoUppercase()
    {
        var policy = StrictPolicy();
        var ex = Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() =>
            CreateSut().ValidatePassword("secure!1pass", policy));
        Assert.Contains("uppercase", ex.Message.ToLower());
    }

    [Fact]
    public void ValidatePassword_Throws_WhenNoLowercase()
    {
        var policy = StrictPolicy();
        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() =>
            CreateSut().ValidatePassword("SECURE!1PASS", policy));
    }

    [Fact]
    public void ValidatePassword_Throws_WhenNoDigit()
    {
        var policy = StrictPolicy();
        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() =>
            CreateSut().ValidatePassword("Secure!pass", policy));
    }

    [Fact]
    public void ValidatePassword_Throws_WhenNoSpecialChar()
    {
        var policy = StrictPolicy();
        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() =>
            CreateSut().ValidatePassword("Secure1pass", policy));
    }

    [Fact]
    public void ValidatePassword_DoesNotThrow_WhenPolicyFlagsOff()
    {
        var policy = new PasswordPolicy
        {
            MinimumLength = 4,
            RequireUppercase = false,
            RequireLowercase = false,
            RequireDigit = false,
            RequireSpecialCharacter = false
        };
        // Should not throw with any password meeting min length
        CreateSut().ValidatePassword("simple", policy);
    }

    [Fact]
    public async Task GetCurrentPolicyAsync_ReturnsNull_WhenNoPolicySet()
    {
        _repo.Setup(r => r.GetCurrentPolicyAsync()).ReturnsAsync((PasswordPolicy?)null);

        var result = await CreateSut().GetCurrentPolicyAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentPolicyAsync_ReturnsPolicy_WhenExists()
    {
        var policy = StrictPolicy();
        _repo.Setup(r => r.GetCurrentPolicyAsync()).ReturnsAsync(policy);

        var result = await CreateSut().GetCurrentPolicyAsync();

        Assert.NotNull(result);
        Assert.Equal(8, result.MinimumLength);
    }
}
