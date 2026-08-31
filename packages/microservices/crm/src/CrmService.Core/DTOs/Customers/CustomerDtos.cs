using CrmService.Core.Enums;

namespace CrmService.Core.DTOs.Customers;

// ── Create / update ──
public class CreateCustomerDto
{
    public string Name { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; } = CustomerType.Company;
    public string? Industry { get; set; }
    public string? Segment { get; set; }
    public AccountTier AccountTier { get; set; } = AccountTier.Standard;
    public string? Geography { get; set; }
    public string? BusinessLine { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ClientReference { get; set; }
    public string? KraPin { get; set; }
    // Account owner defaults to the submitter when omitted.
    public string? AccountOwnerId { get; set; }
    public string? AccountOwnerName { get; set; }
    public string? IntroducedBy { get; set; }
    public List<CreateCustomerContactDto> Contacts { get; set; } = new();
}

public class UpdateCustomerDto
{
    public string? Name { get; set; }
    public string? KraPin { get; set; }
    public CustomerType? CustomerType { get; set; }
    public string? Industry { get; set; }
    public string? Segment { get; set; }
    public AccountTier? AccountTier { get; set; }
    public string? Geography { get; set; }
    public string? BusinessLine { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ClientReference { get; set; }
    public string? AccountOwnerId { get; set; }
    public string? AccountOwnerName { get; set; }
}

// ── Contacts ──
public class CreateCustomerContactDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

public class CustomerContactDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
}

// ── Approval actions ──
public class RejectCustomerDto { public string Reason { get; set; } = string.Empty; }

public class CfoReviewDto
{
    public decimal CreditLimit { get; set; }
    public int CreditTermsDays { get; set; }
    public string? Notes { get; set; }
}

// ── Reads ──
public class CustomerSummaryDto
{
    public string? KraPin { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string AccountTier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string AccountOwnerId { get; set; } = string.Empty;
    public string? AccountOwnerName { get; set; }
    public decimal CreditLimit { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerDetailDto : CustomerSummaryDto
{
    public string? Segment { get; set; }
    public string? Geography { get; set; }
    public string? BusinessLine { get; set; }
    public string? ClientReference { get; set; }
    public string IntroducedBy { get; set; } = string.Empty;
    public bool IntroducedByLocked { get; set; }
    public int CreditTermsDays { get; set; }
    public string SubmittedBy { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string? LineManagerApprovedBy { get; set; }
    public DateTime? LineManagerApprovedAt { get; set; }
    public string? HeadBdApprovedBy { get; set; }
    public DateTime? HeadBdApprovedAt { get; set; }
    public string? CfoApprovedBy { get; set; }
    public DateTime? CfoApprovedAt { get; set; }
    public string? MdApprovedBy { get; set; }
    public DateTime? MdApprovedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? LastInteractionAt { get; set; }
    public DateTime? DormantSince { get; set; }
    public decimal? LastSatisfactionScore { get; set; }
    public int? LastNpsScore { get; set; }
    public List<CustomerContactDto> Contacts { get; set; } = new();
}

public class CustomerFilterParams
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    /// <summary>"active" | "dormant" — engagement state, independent of the approval Status.</summary>
    public string? Activity { get; set; }
    public string? AccountTier { get; set; }
    public string? AccountOwnerId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record CustomerListResult(List<CustomerSummaryDto> Items, int Total);
public record CustomerActionResult(string Status, string Message);
public record DuplicateCheckResult(bool IsDuplicate, List<CustomerSummaryDto> Matches);
