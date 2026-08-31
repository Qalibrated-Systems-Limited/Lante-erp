using TicketingService.Core.DTOs.Comments;
using TicketingService.Core.DTOs.Macros;

namespace TicketingService.Core.Interfaces.Services;

public interface IMacroService
{
    Task<IEnumerable<MacroReadDto>> GetAllAsync(string? categoryId = null);
    Task<MacroReadDto?> GetByIdAsync(string id);
    Task<MacroReadDto> CreateAsync(CreateMacroDto dto, string createdByUserId);
    Task<MacroReadDto> UpdateAsync(string id, UpdateMacroDto dto, string updatedByUserId);
    Task<bool> DeleteAsync(string id);
    Task<CommentReadDto> ApplyToTicketAsync(string ticketId, ApplyMacroDto dto, string agentUserId);
}
