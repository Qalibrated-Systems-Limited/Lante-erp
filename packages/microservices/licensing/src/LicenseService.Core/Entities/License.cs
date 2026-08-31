using System.ComponentModel.DataAnnotations;

namespace LicenseService.Core.Entities;

public class License : BaseEntity
{
    /// <summary>The full ES256 JWT token string — this IS the license key given to the customer.</summary>
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>Customer identifier from Lante CRM.</summary>
    [Required]
    [MaxLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? CustomerName { get; set; }

    /// <summary>
    /// App identifier — must match THIS_APP_ID in the client app's licenseUtils.js.
    /// e.g. "qalitrack-frontend", "qalitrack-mobile"
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string AppId { get; set; } = string.Empty;

    /// <summary>Comma-separated feature flags, e.g. "kiosk,reports".</summary>
    [Required]
    public string Features { get; set; } = string.Empty;

    /// <summary>Optional machine ID for hardware binding. Null = not bound.</summary>
    [MaxLength(255)]
    public string? MachineId { get; set; }

    [Required]
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public bool Revoked { get; set; } = false;

    [MaxLength(500)]
    public string? RevokeReason { get; set; }

    /// <summary>Last time the client app checked in with the server.</summary>
    public DateTime? LastSeen { get; set; }

    /// <summary>Machine ID of the last check-in (audit trail).</summary>
    [MaxLength(255)]
    public string? LastMachineId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Computed helpers (not stored)
    public bool IsExpired => ExpiresAt < DateTime.UtcNow;
    public bool IsActive  => !Revoked && !IsExpired && !IsDeleted;

    public string[] GetFeatures() =>
        string.IsNullOrWhiteSpace(Features)
            ? []
            : Features.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
