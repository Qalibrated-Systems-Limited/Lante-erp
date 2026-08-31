using System.Security.Cryptography;
using System.Text;

namespace UserService.Core.Security;

/// <summary>
/// Single-use invite tokens for the single-login flow. The raw token goes in the emailed link; only
/// its SHA-256 hash is stored in the directory, so a DB leak can't be replayed as a valid invite.
/// </summary>
public static class InviteTokens
{
    /// <summary>A URL-safe 256-bit random token (base64url, no padding).</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>Hex-encoded SHA-256 of the raw token — what we persist and look up by.</summary>
    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
