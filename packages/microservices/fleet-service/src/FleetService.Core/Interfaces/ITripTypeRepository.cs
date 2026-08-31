using FleetService.Core.Entities;

namespace FleetService.Core.Interfaces;

public interface ITripTypeRepository : IRepository<TripType>
{
    Task<(IEnumerable<TripType> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, bool? isActive = null);
}
