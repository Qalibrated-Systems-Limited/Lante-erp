using Microsoft.EntityFrameworkCore;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.Repositories;

public class DriverProfileRepository(FleetServiceDbContext context)
    : Repository<DriverProfile>(context), IDriverProfileRepository
{
    public async Task<IEnumerable<DriverProfile>> GetByDriverIdAsync(string driverId)
        => await _context.DriverProfiles
            .Include(d => d.LicenseClasses)
            .Where(d => d.DriverId == driverId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

    public Task<DriverProfile?> GetCurrentByDriverIdAsync(string driverId)
        => _context.DriverProfiles
            .Include(d => d.LicenseClasses)
            .FirstOrDefaultAsync(d => d.DriverId == driverId && d.IsCurrent);

    public async Task<IEnumerable<DriverProfile>> GetExpiringAsync(DateTime cutoff)
        => await _context.DriverProfiles
            .Where(d => d.IsCurrent && d.LicenseExpiryDate <= cutoff)
            .OrderBy(d => d.LicenseExpiryDate)
            .ToListAsync();

    public async Task<IEnumerable<DriverProfileChange>> GetHistoryByDriverIdAsync(string driverId)
    {
        var profileIds = await _context.DriverProfiles
            .Where(p => p.DriverId == driverId)
            .Select(p => p.Id)
            .ToListAsync();

        return await _context.DriverProfileChanges
            .Where(c => profileIds.Contains(c.NewProfileId))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }
}
