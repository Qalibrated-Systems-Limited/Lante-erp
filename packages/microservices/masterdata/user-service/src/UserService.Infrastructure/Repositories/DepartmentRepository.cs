using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class DepartmentRepository(LanteUserServiceDbContext context)
    : GenericRepository<Department>(context), IDepartmentRepository
{
    public override async Task<IEnumerable<Department>> GetAllAsync()
    {
        return await Context.Departments
            .Include(d => d.Users)
            .ToListAsync();
    }

    public override async Task<Department?> GetByIdAsync(string id, bool includeDeleted = false)
    {
        var query = includeDeleted
            ? Context.Departments.IgnoreQueryFilters()
            : Context.Departments.AsQueryable();

        return await query
            .Include(d => d.Users)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Department?> GetByNameAsync(string name)
    {
        return await Context.Departments.FirstOrDefaultAsync(d => d.Name == name && !d.IsDeleted);
    }

    public async Task<IEnumerable<User>> GetUsersByDepartmentAsync(string departmentId)
    {
        return await Context.Users
            .Where(u => u.DepartmentId == departmentId)
            .ToListAsync();
    }

    public async Task<List<string>> GetDepartmentIdsByGroupAsync(string groupId)
    {
        return await Context.Departments
            .Where(d => d.DepartmentGroupId == groupId && !d.IsDeleted)
            .Select(d => d.Id)
            .ToListAsync();
    }
}
