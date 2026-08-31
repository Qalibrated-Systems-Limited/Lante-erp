using FinanceService.Core.DTOs;

namespace FinanceService.Core.Interfaces;

public interface IImprestService
{
    Task<ImprestReadDto> CreateAsync(CreateImprestDto dto, string? actor);
    Task<ImprestReadDto> ApproveAsync(string id, string? actor, ApprovalContext? ctx = null);
    /// Disburse: post Dr Staff Imprest (1220) / Cr Cash. Stamps the 14-day due date.
    Task<ImprestReadDto> DisburseAsync(string id, string? actor);
    /// Retire: post Dr expense (per line) / Cr Staff Imprest (1220) for the spent portion.
    Task<ImprestReadDto> RetireAsync(string id, RetireImprestDto dto, string? actor);
    /// FIN-012B/C: convert every imprest past its 14-day due date with an unretired balance
    /// into a personal advance flagged for payroll deduction. Returns the advances created.
    Task<List<PersonalAdvanceReadDto>> RunConversionsAsync(DateTime? asOf, string? actor);
    Task<List<ImprestReadDto>> ListAsync(int limit = 200);
    Task<List<PersonalAdvanceReadDto>> ListAdvancesAsync(int limit = 200);
}
