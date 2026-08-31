using FleetService.Core.DTOs.TripDeposit;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface ITripDepositService : IService<TripDeposit>
{
    Task<IEnumerable<TripDeposit>> GetAllAsync(string? tripId = null);
    Task<IEnumerable<TripDeposit>> GetByTripIdAsync(string tripId);
    Task<TripDeposit> CreateFromDtoAsync(CreateTripDepositDto dto);
    Task<(TripDeposit? deposit, string? oldUrl)> UpdateScreenshotAsync(string id, string url);
}

public class TripDepositService(ITripDepositRepository repository) : Service<TripDeposit>(repository), ITripDepositService
{
    private readonly ITripDepositRepository _depositRepo = repository;

    public Task<IEnumerable<TripDeposit>> GetAllAsync(string? tripId = null)
        => _depositRepo.GetAllAsync(tripId);

    public Task<IEnumerable<TripDeposit>> GetByTripIdAsync(string tripId)
        => _depositRepo.GetByTripIdAsync(tripId);

    public Task<TripDeposit> CreateFromDtoAsync(CreateTripDepositDto dto)
        => _repository.CreateAsync(new TripDeposit
        {
            TripId = dto.TripId,
            Amount = dto.Amount,
            BankName = dto.BankName,
            AccountName = dto.AccountName,
            AccountNumber = dto.AccountNumber,
            TransactionDate = dto.TransactionDate,
            MpesaReference = dto.MpesaReference,
            RawSmsText = dto.RawSmsText
        });

    public async Task<(TripDeposit? deposit, string? oldUrl)> UpdateScreenshotAsync(string id, string url)
    {
        var deposit = await _repository.GetByIdAsync(id);
        if (deposit == null) return (null, null);
        var oldUrl = deposit.ScreenshotUrl;
        deposit.ScreenshotUrl = url;
        return (await _repository.UpdateAsync(deposit), oldUrl);
    }
}
