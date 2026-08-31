namespace UserService.Core.Interfaces.Emails;

// Encrypts/decrypts a tenant's SMTP password before it touches the database. Kept as its own
// narrow interface (not folded into IEmailService) so the encryption key never needs to be known
// outside this one boundary — callers just pass plaintext in, ciphertext out, and vice versa.
public interface IEmailCredentialProtector
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
