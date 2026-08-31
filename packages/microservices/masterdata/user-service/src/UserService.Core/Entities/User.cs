using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace UserService.Core.Entities;
public class User : BaseEntity
{
    [Required]
    public string FirstName { get; set; } = string.Empty;
    [Required]
    public string LastName { get; set; } = string.Empty;
    [Required]
    public string Email { get; set; } = string.Empty;
    
    // ✅ Make Password nullable — Google users won't have one
    public string? Password { get; set; }
    
    public string MobileNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = false;
    public bool IsFirstLogin { get; set; } = true;
    public bool TwoFactorEnabled { get; set; } = true;

    // ✅ ADD THESE 3 FIELDS
    public string? GoogleId { get; set; }           // Google's unique user ID
    public string? ProfilePicture { get; set; }     // Google profile photo URL
    public string AuthProvider { get; set; } = "local"; // "local" or "google"

    public string? DepartmentId { get; set; }

    // Schema-per-tenant (Phase 3): a user belongs to exactly one tenant (their schema), so the
    // tenant/branch/admin association moves onto the User itself, collapsing UserTenant.
    // BranchId null = company-wide (HQ / company admin). Populated from UserTenant during migration.
    public string? BranchId { get; set; }
    public bool IsCompanyAdmin { get; set; }

    // Single-login via invites (2026-07): when a company admin creates a user, an invite row lands in
    // the control-plane directory (public.Users) with a hashed, expiring, single-use token. The user
    // sets their own password on accept — no password is emailed. Null once accepted (or for legacy users).
    public string? InviteTokenHash { get; set; }
    public DateTime? InviteTokenExpiresAt { get; set; }

    public virtual Department? Department { get; set; }
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    public virtual ICollection<PersonalAccessToken> PersonalAccessTokens { get; set; } = new List<PersonalAccessToken>();
    public virtual ICollection<UserTenant> UserTenants { get; set; } = [];
    [NotMapped]
    public bool IsOnline => PersonalAccessTokens.Any(t => !t.IsRevoked);
    [NotMapped]
    public int ActiveTokenCount => PersonalAccessTokens.Count(t => !t.IsRevoked);
}