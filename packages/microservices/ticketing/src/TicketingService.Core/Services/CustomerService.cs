using AutoMapper;
using TicketingService.Core.DTOs.Customers;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

/// D8-1 — DEFERRED READ BOUNDARY (CRM Module 6). This service reads the LOCAL Customer table (D1).
/// When CRM Module 6 exists, the plan is to replace/augment this with a CRM CUSTOMER read so the
/// combobox draws from the org-wide customer master. That's intentionally NOT built here — it needs
/// CRM's real read contract; building a speculative client now would be throwaway. To wire it later:
/// add an ICrmCustomerDirectory read seam (mirroring the D8 write seams in Integrations/) and have
/// SearchAsync/GetByIdAsync fall back to it (or prefer it) behind an "Integrations:Crm" flag. The
/// local table remains the safe default. See INTEGRATIONS.md.
public class CustomerService(
    ICustomerRepository customerRepository,
    TicketingService.Core.Integrations.ICrmCustomerDirectory crmDirectory,
    IMapper mapper) : ICustomerService
{
    public async Task<IEnumerable<CustomerReadDto>> SearchAsync(string? query)
    {
        // D8-1 — when the CRM master is wired, source the picker from it so tickets link to the org-wide
        // CUSTOMER register. CRM-sourced rows carry CrmCustomerId (= the CRM id) so the create-ticket flow
        // stamps the local client on selection. Falls back to the local list when CRM is off/unreachable.
        if (crmDirectory.IsEnabled)
        {
            var crm = await crmDirectory.SearchAsync(query);
            if (crm.Count > 0)
                return crm.Select(c => new CustomerReadDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Email = c.Email,
                    Phone = c.Phone,
                    ClientReference = c.ClientReference,
                    IsActive = true,
                    CrmCustomerId = c.Id,
                }).ToList();
        }

        var customers = await customerRepository.SearchAsync(query);
        return mapper.Map<IEnumerable<CustomerReadDto>>(customers);
    }

    public async Task<string> ResolveOrCreateFromCrmAsync(string crmCustomerId, string createdByUserId)
    {
        var existing = await customerRepository.FindByCrmCustomerIdAsync(crmCustomerId);
        if (existing != null) return existing.Id;

        // First link for this CRM customer → create a local mirror stamped with the CrmCustomerId.
        var reference = await crmDirectory.GetByIdAsync(crmCustomerId);
        var customer = new Customer
        {
            Name = reference?.Name ?? "CRM customer",
            Email = reference?.Email,
            Phone = reference?.Phone,
            ClientReference = reference?.ClientReference,
            CrmCustomerId = crmCustomerId,
            CreatedBy = createdByUserId,
            UpdatedBy = createdByUserId,
        };
        var created = await customerRepository.CreateAsync(customer);
        return created.Id;
    }

    public async Task<CustomerReadDto?> GetByIdAsync(string id)
    {
        var c = await customerRepository.GetByIdAsync(id);
        return c == null ? null : mapper.Map<CustomerReadDto>(c);
    }

    public async Task<CustomerReadDto> CreateAsync(CreateCustomerDto dto, string createdByUserId)
    {
        var customer = mapper.Map<Customer>(dto);
        customer.CreatedBy = createdByUserId;
        customer.UpdatedBy = createdByUserId;
        // D8-1b — a client typed into the helpdesk is still the same organisation CRM knows about.
        // Anchor it on the way in so the D8-2/D8-3 write-backs match by id instead of by name.
        customer.CrmCustomerId ??= await ResolveCrmIdAsync(customer.Email);
        var created = await customerRepository.CreateAsync(customer);
        return mapper.Map<CustomerReadDto>(created);
    }

    public async Task<CustomerReadDto> UpdateAsync(string id, UpdateCustomerDto dto, string updatedByUserId)
    {
        var customer = await customerRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Customer {id} not found.");

        if (dto.Name != null) customer.Name = dto.Name;
        if (dto.Company != null) customer.Company = dto.Company;
        if (dto.ClientReference != null) customer.ClientReference = dto.ClientReference;
        if (dto.Email != null) customer.Email = dto.Email;
        if (dto.Phone != null) customer.Phone = dto.Phone;
        if (dto.IsActive.HasValue) customer.IsActive = dto.IsActive.Value;
        customer.UpdatedBy = updatedByUserId;

        // Correcting a typo'd address is the usual reason a previously unresolvable client becomes
        // resolvable. Never re-resolve one that is already anchored: the id is the link the
        // write-backs depend on, and silently repointing it would reassign a client's history.
        if (string.IsNullOrWhiteSpace(customer.CrmCustomerId))
            customer.CrmCustomerId = await ResolveCrmIdAsync(customer.Email);

        var updated = await customerRepository.UpdateAsync(customer);
        return mapper.Map<CustomerReadDto>(updated);
    }

    public Task<bool> DeleteAsync(string id) => customerRepository.DeleteAsync(id);

    public async Task<string> ResolveOrCreateAsync(string name, string? company, string? clientReference, string createdByUserId)
    {
        var existing = await customerRepository.FindByNameAsync(name);
        if (existing != null) return existing.Id;

        var customer = new Customer
        {
            Name = name.Trim(),
            Company = company,
            ClientReference = clientReference,
            CreatedBy = createdByUserId,
            UpdatedBy = createdByUserId,
        };
        var created = await customerRepository.CreateAsync(customer);
        return created.Id;
    }

    /// <summary>
    /// D8-1b — best-effort CRM anchor for a local client. Returns null when CRM is disabled, the
    /// client has no email, or the match is not unambiguous; an unanchored client is normal and
    /// must never block the helpdesk.
    /// </summary>
    private async Task<string?> ResolveCrmIdAsync(string? email)
    {
        if (!crmDirectory.IsEnabled || string.IsNullOrWhiteSpace(email)) return null;
        try { return await crmDirectory.ResolveIdByEmailAsync(email.Trim()); }
        catch { return null; }
    }
}
