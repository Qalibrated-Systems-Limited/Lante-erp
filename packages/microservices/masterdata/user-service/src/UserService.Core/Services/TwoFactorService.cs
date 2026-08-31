using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using UserService.Core.DTOs.Common;
using UserService.Core.Interfaces.Emails;
using UserService.Core.Interfaces.Services;

namespace UserService.Core.Services;

public class TwoFactorService(
    ICacheService cacheService,
    IEmailQueueService emailQueueService,
    ILogger<TwoFactorService> logger)
    : ITwoFactorService
{
    private static readonly RandomNumberGenerator SecureRandom = RandomNumberGenerator.Create();
    private const int MaxAttempts = 5;
    private const int LockoutMinutes = 10;

    public async Task<ServiceResult> GenerateAndSendCodeAsync(string userId, string email)
    {
        var generationKey = $"2fa_generation:{userId}";
        var generationCount = await cacheService.GetAsync<int>(generationKey);

        if (generationCount >= 3)
            return new ServiceResult { Success = false, Message = "Too many 2FA codes requested. Please wait 15 minutes." };

        var code = GenerateSecureCode();
        var codeKey = $"2fa_code:{userId}";
        var attemptKey = $"2fa_attempts:{userId}";

        await Task.WhenAll(
            cacheService.SetAsync(codeKey, code, TimeSpan.FromMinutes(5)),
            cacheService.RemoveAsync(attemptKey),
            cacheService.SetAsync(generationKey, generationCount + 1, TimeSpan.FromMinutes(15))
        );

        var emailSubject = "Your Lante Verification Code";
        var emailBody = $@"
            <html><body style='font-family: Arial, sans-serif;'>
            <h2>Lante Security Verification</h2>
            <p>Your verification code is:</p>
            <h1 style='color: #2196F3; font-size: 32px; text-align: center; padding: 20px; background-color: #f5f5f5; border-radius: 8px;'>{code}</h1>
            <p>This code will expire in <strong>5 minutes</strong>.</p>
            <p>If you didn't request this code, contact your administrator immediately.</p>
            <br><p>Best regards,<br>Lante Security Team</p>
            </body></html>";

        await emailQueueService.EnqueueEmailAsync(email, emailSubject, emailBody);
        logger.LogInformation("2FA code generated and sent for user {UserId}", userId);

        return new ServiceResult { Success = true, Message = "Verification code sent to your email address." };
    }

    public async Task<ServiceResult> VerifyCodeAsync(string sessionId, string code)
    {
        var userId = await GetUserIdFromSessionAsync(sessionId);
        if (string.IsNullOrEmpty(userId))
            return new ServiceResult { Success = false, Message = "Invalid or expired session." };

        var attemptCount = await GetAttemptCountAsync(userId);
        if (attemptCount >= MaxAttempts)
            return new ServiceResult { Success = false, Message = $"Too many failed attempts. Try again in {LockoutMinutes} minutes." };

        var codeKey = $"2fa_code:{userId}";
        var storedCode = await cacheService.GetAsync<string>(codeKey);

        if (string.IsNullOrEmpty(storedCode))
            return new ServiceResult { Success = false, Message = "Verification code has expired. Please request a new code." };

        if (storedCode != code)
        {
            await cacheService.SetAsync($"2fa_attempts:{userId}", attemptCount + 1, TimeSpan.FromMinutes(LockoutMinutes));
            return new ServiceResult { Success = false, Message = $"Invalid code. {MaxAttempts - attemptCount - 1} attempts remaining." };
        }

        await Task.WhenAll(
            cacheService.RemoveAsync(codeKey),
            cacheService.RemoveAsync($"2fa_attempts:{userId}"),
            cacheService.RemoveAsync($"2fa_session:{sessionId}")
        );

        return new ServiceResult { Success = true, Message = "Verification successful." };
    }

    public async Task<string> CreateTwoFactorSessionAsync(string userId, string? schema = null)
    {
        var sessionId = Guid.NewGuid().ToString();
        await cacheService.SetAsync($"2fa_session:{sessionId}", userId, TimeSpan.FromMinutes(10));
        // Remember the tenant schema so verify-2fa reloads the user from the SAME schema it
        // authenticated against (schema-per-tenant), not from public.
        if (!string.IsNullOrEmpty(schema))
            await cacheService.SetAsync($"2fa_schema:{sessionId}", schema, TimeSpan.FromMinutes(10));
        logger.LogInformation("2FA session created for user {UserId} | sessionId={SessionId} | schema={Schema}", userId, sessionId, schema ?? "(public)");
        return sessionId;
    }

    public async Task<string?> GetSchemaFromSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        return await cacheService.GetAsync<string>($"2fa_schema:{sessionId.Trim()}");
    }

    public async Task<string?> GetUserIdFromSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        return await cacheService.GetAsync<string>($"2fa_session:{sessionId.Trim()}");
    }

    public async Task<bool> IsSessionValidAsync(string sessionId)
    {
        var userId = await GetUserIdFromSessionAsync(sessionId);
        return !string.IsNullOrEmpty(userId);
    }

    public async Task<int> GetAttemptCountAsync(string userId)
    {
        return await cacheService.GetAsync<int>($"2fa_attempts:{userId}");
    }

    private static string GenerateSecureCode()
    {
        var randomBytes = new byte[4];
        SecureRandom.GetBytes(randomBytes);
        var randomInt = Math.Abs(BitConverter.ToInt32(randomBytes, 0));
        return ((randomInt % 900000) + 100000).ToString();
    }
}
