using UserService.Core.DTOs.Common;

namespace UserService.Core.Interfaces.Services;

public interface ITwoFactorService
{
    Task<ServiceResult> GenerateAndSendCodeAsync(string userId, string email);
    Task<ServiceResult> VerifyCodeAsync(string sessionId, string code);
    Task<string> CreateTwoFactorSessionAsync(string userId, string? schema = null);
    Task<string?> GetUserIdFromSessionAsync(string sessionId);
    /// <summary>The tenant schema the 2FA session was created for (schema-per-tenant login), or null.</summary>
    Task<string?> GetSchemaFromSessionAsync(string sessionId);
    Task<bool> IsSessionValidAsync(string sessionId);
    Task<int> GetAttemptCountAsync(string userId);
}
