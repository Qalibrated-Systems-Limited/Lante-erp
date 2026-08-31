using CrmService.Core.DTOs.Leads;

namespace CrmService.Core.Interfaces.Services;

public interface ILeadService
{
    Task<LeadListResult> GetAllAsync(LeadFilterParams filter);
    Task<LeadDetailDto?> GetByIdAsync(string id);
    Task<LeadDetailDto> CreateAsync(CreateLeadDto dto, string userId, string? userName);
    Task<LeadDetailDto> UpdateAsync(string id, UpdateLeadDto dto, string userId);
    Task<LeadActivityDto> AddActivityAsync(string id, CreateLeadActivityDto dto, string userId);
    Task<LeadActionResult> AssignAsync(string id, AssignLeadDto dto, string userId);
    Task<LeadActionResult> QualifyAsync(string id, QualifyLeadDto dto, string userId);
    Task<LeadActionResult> UnqualifyAsync(string id, UnqualifyLeadDto dto, string userId);
    // C2: marks the lead converted (+ optional customer link). C3 extends this to also create the
    // opportunity and set ConvertedOpportunityId.
    Task<LeadActionResult> ConvertAsync(string id, string? customerId, string userId);
}
