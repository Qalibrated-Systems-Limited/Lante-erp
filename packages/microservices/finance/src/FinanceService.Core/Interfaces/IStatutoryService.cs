using FinanceService.Core.DTOs;

namespace FinanceService.Core.Interfaces;

public interface IStatutoryService
{
    /// The statutory obligations for a period (yyyy-MM): outstanding GL liability, due date, status.
    Task<List<ObligationDto>> GetObligationsAsync(string period);
    /// Record a remittance: post Dr <liability> / Cr Bank and file the payment reference.
    Task<RemittanceReadDto> RemitAsync(RemitStatutoryDto dto, string? actor);
    Task<List<RemittanceReadDto>> ListRemittancesAsync(int limit = 200);
}
