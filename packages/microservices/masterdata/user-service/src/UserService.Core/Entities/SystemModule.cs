namespace UserService.Core.Entities;

// Per-tenant module registry — lets a tenant admin turn a whole sidebar module off for
// everyone, independent of per-user RBAC permissions (e.g. a tenant with no fleet doesn't
// need the Fleet nav item cluttering every user's sidebar). Core modules can't be disabled.
public class SystemModule : BaseEntity
{
    public string ModuleKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsCore { get; set; }
    public bool IsEnabled { get; set; } = true;
}
