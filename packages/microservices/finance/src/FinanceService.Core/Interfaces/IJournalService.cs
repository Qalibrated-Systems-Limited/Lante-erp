using FinanceService.Core.DTOs;

namespace FinanceService.Core.Interfaces;

/// Journal lifecycle + the backbone posting entry point. Enforces double-entry balance,
/// open-period, direct-posting accounts, and segregation of duties.
public interface IJournalService
{
    Task<JournalReadDto> CreateAsync(CreateJournalDto dto, string? actorUserId);
    Task<JournalReadDto> SubmitForReviewAsync(string id, string? actorUserId);
    Task<JournalReadDto> ReviewAsync(string id, string? actorUserId);
    Task<JournalReadDto> ApproveAndPostAsync(string id, string? actorUserId);
    Task<JournalReadDto> ReverseAsync(string id, string? actorUserId);
    Task<JournalReadDto?> GetAsync(string id);
    Task<List<JournalReadDto>> ListAsync(int limit = 200);
    Task<TrialBalanceDto> GetTrialBalanceAsync(DateTime asOf, string? costCenterId, string? branchId);
}

/// Stubbed FX provider (CBK / Open-FX). The integrations owner swaps in the real adapter later.
public interface IExchangeRateProvider
{
    Task<IReadOnlyDictionary<string, decimal>> FetchRatesAsync(string baseCode, IEnumerable<string> targetCodes);
}
