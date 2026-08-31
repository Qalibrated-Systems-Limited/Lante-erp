using System.ComponentModel.DataAnnotations;

namespace LicenseService.Core.DTOs.Licenses;

// ── Issue ─────────────────────────────────────────────────────────────────────
public class IssueLicenseDto
{
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    /// <summary>e.g. "qalitrack-frontend"</summary>
    [Required]
    public string AppId { get; set; } = string.Empty;

    /// <summary>Feature flags to grant, e.g. ["kiosk", "reports"]</summary>
    [Required]
    [MinLength(1)]
    public string[] Features { get; set; } = [];

    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Optional — binds license to a specific machine ID</summary>
    public string? MachineId { get; set; }

    public string? Notes { get; set; }
}

// ── Validate (called by client apps on check-in) ─────────────────────────────
public class ValidateLicenseDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public string MachineId { get; set; } = string.Empty;

    [Required]
    public string AppId { get; set; } = string.Empty;
}

// ── Revoke ────────────────────────────────────────────────────────────────────
public class RevokeLicenseDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

// ── Renew ─────────────────────────────────────────────────────────────────────
public class RenewLicenseDto
{
    [Required]
    public DateTime NewExpiresAt { get; set; }
}

// ── Read (returned to ERP admin UI) ──────────────────────────────────────────
public class LicenseReadDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string AppId { get; set; } = string.Empty;
    public string[] Features { get; set; } = [];
    public string? MachineId { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public string? RevokeReason { get; set; }
    public DateTime? LastSeen { get; set; }
    public string? LastMachineId { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public int DaysUntilExpiry { get; set; }
    public string Token { get; set; } = string.Empty;
}

// ── Issue response (returns the token so admin can copy it) ──────────────────
public class IssueLicenseResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string[] Features { get; set; } = [];
    public DateTime ExpiresAt { get; set; }
}

// ── Validate response (returned to client app on check-in) ───────────────────
public class ValidateLicenseResponseDto
{
    public bool Valid { get; set; }
    public string? Reason { get; set; }
    public string? CustomerId { get; set; }
    public string? AppId { get; set; }
    public string[]? Features { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool ServerChecked { get; set; } = true;
}
