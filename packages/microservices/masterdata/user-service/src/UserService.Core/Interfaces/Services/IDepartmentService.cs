using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Departments;
using UserService.Core.DTOs.Users;

namespace UserService.Core.Interfaces.Services;

public interface IDepartmentService
{
    Task<IEnumerable<DepartmentReadDto>> GetAllAsync();
    Task<DepartmentReadDto?> GetByIdAsync(string id);
    Task<IEnumerable<UserReadDto>> GetUsersByDepartmentAsync(string departmentId);
    Task<DepartmentReadDto> CreateAsync(CreateDepartmentDto dto);
    Task<DepartmentReadDto?> UpdateAsync(string id, UpdateDepartmentDto dto);
    Task<bool> DeleteAsync(string id);
}
