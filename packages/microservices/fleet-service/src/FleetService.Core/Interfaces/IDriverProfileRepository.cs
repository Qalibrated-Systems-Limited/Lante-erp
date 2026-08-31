using FleetService.Core.Entities;

namespace FleetService.Core.Interfaces;

public interface IDriverProfileRepository : IRepository<DriverProfile>
{
    Task<IEnumerable<DriverProfile>> GetByDriverIdAsync(string driverId);
    Task<DriverProfile?> GetCurrentByDriverIdAsync(string driverId);
    Task<IEnumerable<DriverProfile>> GetExpiringAsync(DateTime cutoff);
    Task<IEnumerable<DriverProfileChange>> GetHistoryByDriverIdAsync(string driverId);
}
