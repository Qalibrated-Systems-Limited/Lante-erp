using UserService.Core.DTOs.Common;
using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface IDepartmentRepository : IGenericRepository<Department>
{
    Task<Department?> GetByNameAsync(string name);
    Task<IEnumerable<User>> GetUsersByDepartmentAsync(string departmentId);
    Task<List<string>> GetDepartmentIdsByGroupAsync(string groupId);
}
