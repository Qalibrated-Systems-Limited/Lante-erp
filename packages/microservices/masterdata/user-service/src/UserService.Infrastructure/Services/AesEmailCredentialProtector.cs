using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using UserService.Core.Interfaces.Emails;

namespace UserService.Infrastructure.Services;

// AES-256-GCM, authenticated so a tampered ciphertext fails to decrypt rather than silently
// producing garbage. Key is SHA-256'd from a configured secret string so ops can set any
// reasonably long value rather than having to hand-generate exactly 32 raw bytes.
public class AesEmailCredentialProtector : IEmailCredentialProtector
{
    private readonly byte[] _key;

    public AesEmailCredentialProtector(IConfiguration configuration)
    {
        var secret = Environment.GetEnvironmentVariable("TENANT_SECRETS_ENCRYPTION_KEY")
                     ?? configuration["Encryption:TenantSecretsKey"]
                     ?? throw new InvalidOperationException(
                         "Encryption:TenantSecretsKey (or TENANT_SECRETS_ENCRYPTION_KEY) is not configured — " +
                         "required to store/read a tenant's SMTP password.");
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // nonce || tag || ciphertext, base64'd as one opaque blob.
        var combined = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, nonce.Length + tag.Length, cipherBytes.Length);
        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string ciphertext)
    {
        var combined = Convert.FromBase64String(ciphertext);
        var nonceLen = AesGcm.NonceByteSizes.MaxSize;
        var tagLen = AesGcm.TagByteSizes.MaxSize;

        var nonce = combined[..nonceLen];
        var tag = combined[nonceLen..(nonceLen + tagLen)];
        var cipherBytes = combined[(nonceLen + tagLen)..];
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, tagLen);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
