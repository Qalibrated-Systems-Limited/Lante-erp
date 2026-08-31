namespace TicketingService.Core.Entities;

/// <summary>
/// Temporary holding record for a portal service/calibration request form
/// that is awaiting OTP email verification. Promoted to ServiceRequest on success.
/// </summary>
public class PendingVerification : BaseEntity
{
    public string Email          { get; set; } = string.Empty;
    public string OtpCode        { get; set; } = string.Empty; // bcrypt hash stored here
    public DateTime ExpiresAt    { get; set; }
    public string FormType       { get; set; } = string.Empty; // ServiceRequestFormType.ToString()
    public string FormDataJson   { get; set; } = string.Empty; // full form payload as JSON
    public bool   IsVerified     { get; set; } = false;
    public DateTime? VerifiedAt  { get; set; }
    public int AttemptCount      { get; set; } = 0;
}
