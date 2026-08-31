using UserService.Core.DTOs.Modules;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;

namespace UserService.Core.Services;

public class SystemModuleService(ISystemModuleRepository repository) : ISystemModuleService
{
    // Mirrors AppShell.jsx's NAV item ids/labels (excluding Helpdesk's internal sub-nav, which
    // isn't independently toggleable). Core modules can't be disabled — matches the reference
    // design's "Dashboard, Admin, Settings cannot be disabled" rule (Settings now lives inside
    // Administration itself, so Admin covers it).
    private static readonly (string Key, string Label, bool IsCore)[] DefaultModules =
    [
        ("dashboard", "Dashboard", true),
        ("me", "My Workspace", true),
        ("admin", "Administration", true),
        ("helpdesk", "Tickets", false),
        ("finance", "Finance", false),
        ("debtors", "Debtors", false),
        ("tax", "Tax & KRA", false),
        ("assets", "Fixed Assets", false),
        ("ic", "Inter-Company", false),
        ("crm", "Commercial", false),
        ("bids", "Bids & Pre-Sales", false),
        ("shop", "Online Shop", false),
        ("procurement", "Procurement", false),
        ("stores", "Stores", false),
        ("requisitions", "Requisitions", false),
        ("calibration", "Technical Department", false),
        ("service-requests", "Service Requests", false),
        ("inspection", "Inspection (17020)", false),
        ("projects", "Projects", false),
        ("fleet", "Fleet", false),
        ("hse", "Health and Safety", false),
        ("subcontracts", "Subcontracts", false),
        ("tasks", "Tasks", false),
        ("hr", "HR & Payroll", false),
        ("quality", "Quality (QMS)", false),
        ("compliance", "Compliance", false),
        ("sops", "SOP Library", false),
        ("reports", "Reports", false),
        ("licensing", "Licensing", false),
    ];

    public async Task<IEnumerable<SystemModuleDto>> GetAllAsync()
    {
        var existing = (await repository.GetAllAsync()).ToDictionary(m => m.ModuleKey);

        // Seed any module missing from this tenant's schema — covers first-ever call and any
        // module added to DefaultModules after a tenant was already provisioned.
        foreach (var (key, label, isCore) in DefaultModules)
        {
            if (existing.ContainsKey(key)) continue;
            var created = new SystemModule { ModuleKey = key, DisplayName = label, IsCore = isCore, IsEnabled = true };
            await repository.CreateAsync(created);
            existing[key] = created;
        }

        return DefaultModules.Select(d => ToDto(existing[d.Key]));
    }

    public async Task<SystemModuleDto> ToggleAsync(string moduleKey, bool enabled)
    {
        var module = await repository.GetByKeyAsync(moduleKey)
            ?? throw new KeyNotFoundException($"Module '{moduleKey}' not found.");
        if (module.IsCore)
            throw new InvalidOperationException("Core modules cannot be disabled.");

        module.IsEnabled = enabled;
        await repository.UpdateAsync(module);
        return ToDto(module);
    }

    private static SystemModuleDto ToDto(SystemModule m) => new(m.ModuleKey, m.DisplayName, m.IsCore, m.IsEnabled);
}
