using AutoMapper;
using TicketingService.Core.DTOs.Comments;
using TicketingService.Core.DTOs.Macros;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

public class MacroService(
    IMacroRepository macroRepository,
    ITicketRepository ticketRepository,
    ITicketCommentRepository commentRepository,
    ITicketHistoryService historyService,
    IMapper mapper) : IMacroService
{
    public async Task<IEnumerable<MacroReadDto>> GetAllAsync(string? categoryId = null)
    {
        if (categoryId != null)
        {
            var byCategory = await macroRepository.GetByCategoryAsync(categoryId);
            var global = await macroRepository.GetGlobalAsync();
            return mapper.Map<IEnumerable<MacroReadDto>>(byCategory.Union(global));
        }

        var all = await macroRepository.GetAllAsync();
        return mapper.Map<IEnumerable<MacroReadDto>>(all);
    }

    public async Task<MacroReadDto?> GetByIdAsync(string id)
    {
        var macro = await macroRepository.GetByIdAsync(id);
        return macro == null ? null : mapper.Map<MacroReadDto>(macro);
    }

    public async Task<MacroReadDto> CreateAsync(CreateMacroDto dto, string createdByUserId)
    {
        var macro = mapper.Map<Macro>(dto);
        macro.CreatedBy = createdByUserId;
        macro.UpdatedBy = createdByUserId;

        var created = await macroRepository.CreateAsync(macro);
        return mapper.Map<MacroReadDto>(created);
    }

    public async Task<MacroReadDto> UpdateAsync(string id, UpdateMacroDto dto, string updatedByUserId)
    {
        var macro = await macroRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Macro {id} not found.");

        if (dto.Name != null) macro.Name = dto.Name;
        if (dto.Description != null) macro.Description = dto.Description;
        if (dto.Content != null) macro.Content = dto.Content;
        if (dto.CategoryId != null) macro.CategoryId = dto.CategoryId;
        if (dto.IsGlobal.HasValue) macro.IsGlobal = dto.IsGlobal.Value;
        macro.UpdatedBy = updatedByUserId;

        var updated = await macroRepository.UpdateAsync(macro);
        return mapper.Map<MacroReadDto>(updated);
    }

    public Task<bool> DeleteAsync(string id) => macroRepository.DeleteAsync(id);

    public async Task<CommentReadDto> ApplyToTicketAsync(string ticketId, ApplyMacroDto dto, string agentUserId)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} not found.");

        var macro = await macroRepository.GetByIdAsync(dto.MacroId)
            ?? throw new KeyNotFoundException($"Macro {dto.MacroId} not found.");

        var comment = new TicketComment
        {
            TicketId = ticketId,
            AuthorUserId = agentUserId,
            Content = macro.Content,
            IsInternal = dto.IsInternal,
            CreatedBy = agentUserId
        };

        var created = await commentRepository.CreateAsync(comment);

        await historyService.AppendAsync(ticketId, agentUserId, "MacroApplied", null, macro.Name,
            dto.IsInternal ? "Internal note from macro" : "Reply from macro");

        return mapper.Map<CommentReadDto>(created);
    }
}
