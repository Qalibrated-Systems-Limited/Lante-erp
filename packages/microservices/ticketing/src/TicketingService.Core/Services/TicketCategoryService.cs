using AutoMapper;
using TicketingService.Core.DTOs.Categories;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

public class TicketCategoryService(ITicketCategoryRepository categoryRepository, IMapper mapper) : ITicketCategoryService
{
    public async Task<IEnumerable<CategoryReadDto>> GetAllAsync()
    {
        var categories = await categoryRepository.GetAllAsync();
        return mapper.Map<IEnumerable<CategoryReadDto>>(categories);
    }

    public async Task<CategoryReadDto?> GetByIdAsync(string id)
    {
        var category = await categoryRepository.GetByIdAsync(id);
        return category == null ? null : mapper.Map<CategoryReadDto>(category);
    }

    public async Task<CategoryReadDto> CreateAsync(CreateCategoryDto dto, string createdByUserId)
    {
        var existing = await categoryRepository.GetByNameAsync(dto.Name);
        if (existing != null)
            throw new InvalidOperationException($"Category '{dto.Name}' already exists.");

        var category = mapper.Map<TicketCategory>(dto);
        category.CreatedBy = createdByUserId;
        category.UpdatedBy = createdByUserId;

        var created = await categoryRepository.CreateAsync(category);
        return mapper.Map<CategoryReadDto>(created);
    }

    public async Task<CategoryReadDto> UpdateAsync(string id, UpdateCategoryDto dto, string updatedByUserId)
    {
        var category = await categoryRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Category {id} not found.");

        if (dto.Name != null) category.Name = dto.Name;
        if (dto.Description != null) category.Description = dto.Description;
        if (dto.DepartmentId != null) category.DepartmentId = dto.DepartmentId;
        if (dto.DefaultPriority.HasValue) category.DefaultPriority = dto.DefaultPriority.Value;
        if (dto.DefaultAssigneeId != null) category.DefaultAssigneeId = dto.DefaultAssigneeId;
        if (dto.RequiresEvidence.HasValue) category.RequiresEvidence = dto.RequiresEvidence.Value;
        if (dto.AutoCreateANCR.HasValue) category.AutoCreateANCR = dto.AutoCreateANCR.Value;
        if (dto.IsActive.HasValue) category.IsActive = dto.IsActive.Value;

        category.UpdatedBy = updatedByUserId;
        var updated = await categoryRepository.UpdateAsync(category);
        return mapper.Map<CategoryReadDto>(updated);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var category = await categoryRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Category {id} not found.");
        return await categoryRepository.DeleteAsync(id);
    }
}
