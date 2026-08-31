namespace TicketingService.Core.DTOs.Customers;

public class CustomerReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? ClientReference { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    /// D8-1 — set on local rows linked to the CRM master, and on CRM-sourced picker results (= the CRM
    /// customer id). The create-ticket flow sends this back so the ticket links to the CRM customer.
    public string? CrmCustomerId { get; set; }
}

public class CreateCustomerDto
{
    public string Name { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? ClientReference { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class UpdateCustomerDto
{
    public string? Name { get; set; }
    public string? Company { get; set; }
    public string? ClientReference { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool? IsActive { get; set; }
}
