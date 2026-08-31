using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.ToolboxTalks;
using HSEService.Core.Entities;

namespace HSEService.Core.Interfaces.Services;

// HSE-004: creating a talk and sign-off attendees is one atomic-from-the-caller's-view action.
public interface IToolboxTalkWorkflowService
{
    Task<ToolboxTalk> CreateWithAttendeesAsync(CreateToolboxTalkDto dto);
    Task<ToolboxTalk?> GetWithAttendeesAsync(string id);
    Task<List<ToolboxTalk>> GetAllWithAttendeesAsync();
    Task<PaginatedResult<ToolboxTalk>> GetPagedWithAttendeesAsync(PaginationParameters parameters);
}
