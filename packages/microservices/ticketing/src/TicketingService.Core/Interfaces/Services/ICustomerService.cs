using TicketingService.Core.DTOs.Customers;

namespace TicketingService.Core.Interfaces.Services;

public interface ICustomerService
{
    Task<IEnumerable<CustomerReadDto>> SearchAsync(string? query);
    Task<CustomerReadDto?> GetByIdAsync(string id);
    Task<CustomerReadDto> CreateAsync(CreateCustomerDto dto, string createdByUserId);
    Task<CustomerReadDto> UpdateAsync(string id, UpdateCustomerDto dto, string updatedByUserId);
    Task<bool> DeleteAsync(string id);
    /// D1-3 — resolve a client the user typed in: reuse an existing one by (case-insensitive) name,
    /// otherwise create a lightweight new customer. Returns the customer id.
    Task<string> ResolveOrCreateAsync(string name, string? company, string? clientReference, string createdByUserId);

    /// D8-1 — resolve a CRM customer master id to a local client row (creating/stamping it with the
    /// CrmCustomerId on first use). Returns the local Customer id the ticket should reference.
    Task<string> ResolveOrCreateFromCrmAsync(string crmCustomerId, string createdByUserId);
}
