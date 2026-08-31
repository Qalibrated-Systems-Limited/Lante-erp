using FleetService.Core.Entities;

namespace FleetService.Core.Interfaces;

public interface IMaterialRepository : IRepository<Material>
{
    Task<IEnumerable<Material>> GetAllWithDetailsAsync();
    Task<Material?> GetByIdWithDetailsAsync(string id);
    Task<(IEnumerable<Material> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? search = null);
}
