using Microsoft.EntityFrameworkCore;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.Repositories;

public class TripDepositRepository(FleetServiceDbContext context)
    : Repository<TripDeposit>(context), ITripDepositRepository
{
    public async Task<IEnumerable<TripDeposit>> GetAllAsync(string? tripId = null)
    {
        var query = _context.TripDeposits.AsQueryable();
        if (!string.IsNullOrEmpty(tripId)) query = query.Where(d => d.TripId == tripId);
        return await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<TripDeposit>> GetByTripIdAsync(string tripId)
        => await _context.TripDeposits
            .Where(d => d.TripId == tripId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
}
