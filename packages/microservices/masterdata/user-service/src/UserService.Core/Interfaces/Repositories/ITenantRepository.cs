using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface ITenantRepository
{
    Task<UserTenant?> GetDefaultTenantForUserAsync(string userId);
    Task<string?> GetHqBranchIdForTenantAsync(string tenantId);
    Task<Tenant?> GetByIdAsync(string tenantId);
    Task<Tenant?> GetBySlugAsync(string slug);
    Task<Tenant?> GetBySchemaAsync(string schemaName);
    Task<Branch?> GetBranchByIdAsync(string branchId);
    Task<List<Tenant>> GetAllTenantsAsync();
    Task<List<Branch>> GetBranchesByTenantAsync(string tenantId);
    Task<Tenant> CreateTenantAsync(Tenant tenant);
    Task<Branch> CreateBranchAsync(Branch branch);
    Task<UserTenant> CreateUserTenantAsync(UserTenant userTenant);
    Task<UserTenant?> GetUserTenantAsync(string userId, string tenantId);
    Task SaveChangesAsync();
}
