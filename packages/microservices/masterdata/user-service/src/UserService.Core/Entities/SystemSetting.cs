namespace UserService.Core.Entities;

// Generic per-tenant key/value settings registry (company identity, branding, finance limits,
// MSP margins, alert windows, banking, etc.) — tenant-scoped like Department/PasswordPolicy,
// resolved via the request's search_path rather than a TenantId column.
public class SystemSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
