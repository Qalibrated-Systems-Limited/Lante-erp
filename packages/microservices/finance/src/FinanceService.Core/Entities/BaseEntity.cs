namespace FinanceService.Core.Entities;

/// Shared columns on every finance table. Matches the platform convention (string GUID id,
/// TenantId for the schema-per-tenant model, audit timestamps).
///
/// No soft-delete column: finance has no delete path anywhere in the service (#332) -- nothing
/// ever wrote IsDeleted, and 21 of these entities carried a HasQueryFilter checking a column that
/// could never be anything but false. Removed rather than wired up, per #332's own finding that
/// removing an inert guard is one of the two acceptable outcomes when nothing depends on it.
public abstract class BaseEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
