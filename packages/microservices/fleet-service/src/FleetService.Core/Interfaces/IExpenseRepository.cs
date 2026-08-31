using FleetService.Core.Entities;

namespace FleetService.Core.Interfaces;

public interface IExpenseRepository : IRepository<Expense>
{
    Task<IEnumerable<Expense>> GetAllAsync(string? tripId = null);
    Task<IEnumerable<Expense>> GetByTripIdAsync(string tripId);
}
