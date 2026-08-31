namespace CrmService.Core.Entities;

/// <summary>P1 — CUSTOMER_CONTACT. Contact hierarchy per client; one primary contact.</summary>
public class CustomerContact : BaseEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;

    public Customer? Customer { get; set; }
}
