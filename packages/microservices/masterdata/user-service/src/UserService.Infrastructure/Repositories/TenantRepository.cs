using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class TenantRepository(LanteUserServiceDbContext context) : ITenantRepository
{
    public async Task<UserTenant?> GetDefaultTenantForUserAsync(string userId)
    {
        return await context.UserTenants
            .Include(ut => ut.Tenant)
            .Include(ut => ut.Branch)
            .Where(ut => ut.UserId == userId && ut.IsDefault && !ut.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<string?> GetHqBranchIdForTenantAsync(string tenantId)
    {
        var hq = await context.Branches
            .Where(b => b.TenantId == tenantId && b.IsHeadOffice && b.IsActive && !b.IsDeleted)
            .FirstOrDefaultAsync();
        return hq?.Id;
    }

    public async Task<Tenant?> GetByIdAsync(string tenantId)
        => await context.Tenants.FindAsync(tenantId);

    public async Task<Tenant?> GetBySlugAsync(string slug)
        => await context.Tenants.FirstOrDefaultAsync(t => t.Slug == slug && !t.IsDeleted);

    public async Task<Tenant?> GetBySchemaAsync(string schemaName)
        => await context.Tenants.FirstOrDefaultAsync(t => t.SchemaName == schemaName && !t.IsDeleted);

    public async Task<Branch?> GetBranchByIdAsync(string branchId)
        => await context.Branches.FindAsync(branchId);

    public async Task<List<Tenant>> GetAllTenantsAsync()
        => await context.Tenants.Where(t => !t.IsDeleted).ToListAsync();

    public async Task<List<Branch>> GetBranchesByTenantAsync(string tenantId)
        => await context.Branches
            .Where(b => b.TenantId == tenantId && !b.IsDeleted)
            .OrderBy(b => b.Name)
            .ToListAsync();

    public async Task<Tenant> CreateTenantAsync(Tenant tenant)
    {
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant;
    }

    public async Task<Branch> CreateBranchAsync(Branch branch)
    {
        context.Branches.Add(branch);
        await context.SaveChangesAsync();
        return branch;
    }

    public async Task<UserTenant> CreateUserTenantAsync(UserTenant userTenant)
    {
        context.UserTenants.Add(userTenant);
        await context.SaveChangesAsync();
        return userTenant;
    }

    public async Task<UserTenant?> GetUserTenantAsync(string userId, string tenantId)
    {
        return await context.UserTenants
            .Where(ut => ut.UserId == userId && ut.TenantId == tenantId && !ut.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task SaveChangesAsync()
        => await context.SaveChangesAsync();
}
