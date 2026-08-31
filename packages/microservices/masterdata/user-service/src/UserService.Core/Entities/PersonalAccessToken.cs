using System.ComponentModel.DataAnnotations;

namespace UserService.Core.Entities;

public class PersonalAccessToken : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    public virtual User User { get; set; } = null!;

    // No StringLength here: the generated JWT grows with the user's role/permission/department
    // claim count and can exceed any fixed cap (see TenantDbContext's HasColumnType("text")) —
    // a StringLength attribute would get picked up by EF's convention discovery and silently
    // reintroduce the same cap on the next migration scaffold.
    [Required]
    public string Token { get; set; } = string.Empty;

    public string Jti { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? BranchId { get; set; }

    [Required]
    public bool IsRevoked { get; set; } = false;

    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
