using AutoMapper;
using Microsoft.Extensions.Logging;
using UserService.Core.DTOs.Departments;
using UserService.Core.DTOs.Users;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;

namespace UserService.Core.Services;

public class DepartmentService(
    IDepartmentRepository departmentRepository,
    IMapper mapper)
    : IDepartmentService
{
    public async Task<IEnumerable<DepartmentReadDto>> GetAllAsync()
    {
        var depts = await departmentRepository.GetAllAsync();
        return mapper.Map<IEnumerable<DepartmentReadDto>>(depts);
    }

    public async Task<DepartmentReadDto?> GetByIdAsync(string id)
    {
        var dept = await departmentRepository.GetByIdAsync(id);
        return dept == null ? null : mapper.Map<DepartmentReadDto>(dept);
    }

    public async Task<IEnumerable<UserReadDto>> GetUsersByDepartmentAsync(string departmentId)
    {
        var users = await departmentRepository.GetUsersByDepartmentAsync(departmentId);
        return mapper.Map<IEnumerable<UserReadDto>>(users);
    }

    public async Task<DepartmentReadDto> CreateAsync(CreateDepartmentDto dto)
    {
        var existing = await departmentRepository.GetByNameAsync(dto.Name);
        if (existing != null)
            throw new InvalidOperationException($"Department '{dto.Name}' already exists.");

        var dept = new Department
        {
            Name = dto.Name,
            Description = dto.Description,
            IsActive = dto.IsActive,
            DepartmentGroupId = string.IsNullOrWhiteSpace(dto.DepartmentGroupId) ? null : dto.DepartmentGroupId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await departmentRepository.CreateAsync(dept);
        return mapper.Map<DepartmentReadDto>(dept);
    }

    public async Task<DepartmentReadDto?> UpdateAsync(string id, UpdateDepartmentDto dto)
    {
        var dept = await departmentRepository.GetByIdAsync(id);
        if (dept == null) throw new KeyNotFoundException($"Department {id} not found.");

        if (dto.Name != null) dept.Name = dto.Name;
        if (dto.Description != null) dept.Description = dto.Description;
        if (dto.IsActive.HasValue) dept.IsActive = dto.IsActive.Value;
        if (dto.DepartmentGroupId != null) dept.DepartmentGroupId = dto.DepartmentGroupId == "" ? null : dto.DepartmentGroupId;
        dept.UpdatedAt = DateTime.UtcNow;

        await departmentRepository.UpdateAsync(dept);
        return mapper.Map<DepartmentReadDto>(dept);
    }

    public async Task<bool> DeleteAsync(string id) => await departmentRepository.DeleteAsync(id);
}
