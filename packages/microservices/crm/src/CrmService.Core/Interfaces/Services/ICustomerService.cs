using CrmService.Core.DTOs.Customers;

namespace CrmService.Core.Interfaces.Services;

public interface ICustomerService
{
    Task<CustomerListResult> GetAllAsync(CustomerFilterParams filter);
    Task<CustomerDetailDto?> GetByIdAsync(string id);
    Task<DuplicateCheckResult> CheckDuplicateAsync(string? name, string? email, string? phone, string? kraPin = null);

    Task<CustomerDetailDto> CreateAsync(CreateCustomerDto dto, string userId, string? userName);
    Task<CustomerDetailDto> UpdateAsync(string id, UpdateCustomerDto dto, string userId);

    // 4-stage onboarding (LineManager → Head of BD → CFO → MD).
    Task<CustomerActionResult> ApproveLineManagerAsync(string id, string userId);
    Task<CustomerActionResult> ApproveHeadBdAsync(string id, string userId);
    Task<CustomerActionResult> CfoReviewAsync(string id, CfoReviewDto dto, string userId);
    Task<CustomerActionResult> ApproveMdAsync(string id, string userId);
    Task<CustomerActionResult> RejectAsync(string id, RejectCustomerDto dto, string userId);
    Task<CustomerActionResult> DeactivateAsync(string id, string userId);

    // Contacts.
    Task<CustomerContactDto> AddContactAsync(string customerId, CreateCustomerContactDto dto, string userId);
    Task<CustomerContactDto> UpdateContactAsync(string contactId, CreateCustomerContactDto dto, string userId);
}
