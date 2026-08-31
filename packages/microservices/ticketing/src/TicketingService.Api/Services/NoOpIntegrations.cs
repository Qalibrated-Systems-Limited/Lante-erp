using TicketingService.Core.Integrations;

namespace TicketingService.Api.Services;

// D8 — default no-op integration adapters. They log intent when their integration flag is enabled
// (so the seams are observable end-to-end) and are otherwise inert. Swap these registrations for
// real HTTP adapters once CRM Module 6 / HR Module 3 exist. See INTEGRATIONS.md.

/// D8-2 / D8-3 — no-op CRM sink. Enabled via config "Integrations:Crm:Enabled".
public class NoOpCrmSync(IConfiguration config, ILogger<NoOpCrmSync> logger) : ICrmSync
{
    public bool IsEnabled =>
        config.GetSection("Integrations:Crm")["Enabled"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    public Task RecordCustomerInteractionAsync(CustomerInteraction interaction, CancellationToken ct = default)
    {
        if (IsEnabled)
            logger.LogInformation("CRM (stub) customer interaction: {Type} on {Ref} (customer {Customer})",
                interaction.InteractionType, interaction.TicketReference, interaction.CustomerId ?? "—");
        return Task.CompletedTask;
    }

    public Task RecordComplaintClosureAsync(ComplaintClosure closure, CancellationToken ct = default)
    {
        if (IsEnabled)
            logger.LogInformation("CRM (stub) complaint register: {Ref} closed (customer {Customer})",
                closure.TicketReference, closure.CustomerId ?? "—");
        return Task.CompletedTask;
    }
}

/// D8-5 — no-op HR employee directory. Enabled via config "Integrations:Hr:Enabled".
public class NoOpEmployeeDirectory(IConfiguration config, ILogger<NoOpEmployeeDirectory> logger) : IEmployeeDirectory
{
    public bool IsEnabled =>
        config.GetSection("Integrations:Hr")["Enabled"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    public Task<EmployeeRef?> ResolveAsync(string employeeId, CancellationToken ct = default)
    {
        if (IsEnabled)
            logger.LogInformation("HR (stub) employee lookup: {EmployeeId} — no directory wired yet", employeeId);
        return Task.FromResult<EmployeeRef?>(null);
    }
}
