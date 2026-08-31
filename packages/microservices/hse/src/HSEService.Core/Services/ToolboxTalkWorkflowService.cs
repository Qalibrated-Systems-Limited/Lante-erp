using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.ToolboxTalks;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Core.Services;

public class ToolboxTalkWorkflowService(
    IHseCrudService<ToolboxTalk> talks,
    IHseCrudService<ToolboxAttendee> attendees) : IToolboxTalkWorkflowService
{
    public async Task<ToolboxTalk> CreateWithAttendeesAsync(CreateToolboxTalkDto dto)
    {
        var talk = new ToolboxTalk
        {
            SiteId = dto.SiteId,
            SiteName = dto.SiteName,
            SupervisorUserId = dto.SupervisorUserId,
            SupervisorName = dto.SupervisorName,
            Topic = dto.Topic,
            HeldOn = dto.HeldOn,
        };
        await talks.CreateAsync(talk);

        foreach (var a in dto.Attendees)
        {
            var attendee = new ToolboxAttendee
            {
                TalkId = talk.Id,
                EmployeeUserId = a.EmployeeUserId,
                EmployeeName = a.EmployeeName,
            };
            await attendees.CreateAsync(attendee);
            talk.Attendees.Add(attendee);
        }

        return talk;
    }

    public async Task<ToolboxTalk?> GetWithAttendeesAsync(string id)
    {
        var talk = await talks.GetByIdAsync(id);
        if (talk is null) return null;
        talk.Attendees = await attendees.FindAsync(a => a.TalkId == id);
        return talk;
    }

    public async Task<List<ToolboxTalk>> GetAllWithAttendeesAsync()
    {
        var all = (await talks.GetAllAsync()).OrderByDescending(t => t.HeldOn).ToList();
        foreach (var talk in all)
            talk.Attendees = await attendees.FindAsync(a => a.TalkId == talk.Id);
        return all;
    }

    public async Task<PaginatedResult<ToolboxTalk>> GetPagedWithAttendeesAsync(PaginationParameters parameters)
    {
        var paged = await talks.GetPagedAsync(parameters);
        var items = paged.Items.ToList();
        foreach (var talk in items)
            talk.Attendees = await attendees.FindAsync(a => a.TalkId == talk.Id);

        return new PaginatedResult<ToolboxTalk>
        {
            Items = items,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }
}
