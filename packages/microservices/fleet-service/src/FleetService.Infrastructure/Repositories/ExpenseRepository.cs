using Microsoft.EntityFrameworkCore;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.Repositories;

public class ExpenseRepository(FleetServiceDbContext context)
    : Repository<Expense>(context), IExpenseRepository
{
    public async Task<IEnumerable<Expense>> GetAllAsync(string? tripId = null)
    {
        var query = _context.Expenses.AsQueryable();
        if (!string.IsNullOrEmpty(tripId)) query = query.Where(e => e.TripId == tripId);
        return await query.OrderByDescending(e => e.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<Expense>> GetByTripIdAsync(string tripId)
        => await _context.Expenses
            .Where(e => e.TripId == tripId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
}
