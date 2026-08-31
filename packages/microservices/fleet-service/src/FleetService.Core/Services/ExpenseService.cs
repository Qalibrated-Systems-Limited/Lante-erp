using FleetService.Core.DTOs.Expense;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface IExpenseService : IService<Expense>
{
    Task<IEnumerable<Expense>> GetAllAsync(string? tripId = null);
    Task<IEnumerable<Expense>> GetByTripIdAsync(string tripId);
    Task<Expense> CreateFromDtoAsync(CreateExpenseDto dto);
    Task<Expense?> UpdateDetailsAsync(string id, string description, decimal amount);
    Task<(Expense? expense, string? oldUrl)> UpdateReceiptPhotoAsync(string id, string url);
}

public class ExpenseService(IExpenseRepository repository) : Service<Expense>(repository), IExpenseService
{
    private readonly IExpenseRepository _expenseRepo = repository;

    public Task<IEnumerable<Expense>> GetAllAsync(string? tripId = null)
        => _expenseRepo.GetAllAsync(tripId);

    public Task<IEnumerable<Expense>> GetByTripIdAsync(string tripId)
        => _expenseRepo.GetByTripIdAsync(tripId);

    public Task<Expense> CreateFromDtoAsync(CreateExpenseDto dto)
        => _repository.CreateAsync(new Expense
        {
            TripId = dto.TripId,
            Description = dto.Description,
            Amount = dto.Amount
        });

    public async Task<Expense?> UpdateDetailsAsync(string id, string description, decimal amount)
    {
        var expense = await _repository.GetByIdAsync(id);
        if (expense == null) return null;
        expense.Description = description;
        expense.Amount = amount;
        return await _repository.UpdateAsync(expense);
    }

    public async Task<(Expense? expense, string? oldUrl)> UpdateReceiptPhotoAsync(string id, string url)
    {
        var expense = await _repository.GetByIdAsync(id);
        if (expense == null) return (null, null);
        var oldUrl = expense.ReceiptPhotoUrl;
        expense.ReceiptPhotoUrl = url;
        return (await _repository.UpdateAsync(expense), oldUrl);
    }
}
