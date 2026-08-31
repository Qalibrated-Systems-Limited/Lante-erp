using FleetService.Core.Entities;

namespace FleetService.Core.Interfaces;

public interface ITripDepositRepository : IRepository<TripDeposit>
{
    Task<IEnumerable<TripDeposit>> GetAllAsync(string? tripId = null);
    Task<IEnumerable<TripDeposit>> GetByTripIdAsync(string tripId);
}
