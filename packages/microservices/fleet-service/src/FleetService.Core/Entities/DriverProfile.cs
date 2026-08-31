namespace FleetService.Core.Entities;

public class DriverProfile : BaseEntity
{
    public string DriverId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public DateTime LicenseExpiryDate { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public string? LicenseFrontImageUrl { get; set; }
    public string? LicenseBackImageUrl { get; set; }
    public string? IdFrontImageUrl { get; set; }
    public string? IdBackImageUrl { get; set; }
    public DriverProfileStatus Status { get; set; } = DriverProfileStatus.Draft;
    public bool IsCurrent { get; set; } = true;
    public int VersionNumber { get; set; } = 1;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ApprovalNotes { get; set; }
    public string? RejectionReason { get; set; }

    public int DaysUntilLicenseExpiry => (int)(LicenseExpiryDate - DateTime.UtcNow).TotalDays;

    public virtual ICollection<LicenseClass> LicenseClasses { get; set; } = new List<LicenseClass>();
    public virtual ICollection<DriverProfileChange> Changes { get; set; } = new List<DriverProfileChange>();
}

public enum DriverProfileStatus { Draft, Pending, Approved, Rejected, ChangesRequested }
