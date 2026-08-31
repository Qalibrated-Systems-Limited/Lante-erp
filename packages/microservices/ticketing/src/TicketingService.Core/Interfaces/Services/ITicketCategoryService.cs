using TicketingService.Core.DTOs.Categories;

namespace TicketingService.Core.Interfaces.Services;

public interface ITicketCategoryService
{
    Task<IEnumerable<CategoryReadDto>> GetAllAsync();
    Task<CategoryReadDto?> GetByIdAsync(string id);
    Task<CategoryReadDto> CreateAsync(CreateCategoryDto dto, string createdByUserId);
    Task<CategoryReadDto> UpdateAsync(string id, UpdateCategoryDto dto, string updatedByUserId);
    Task<bool> DeleteAsync(string id);
}
