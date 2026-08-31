using TicketingService.Core.DTOs.Comments;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.Tickets;
using TicketingService.Core.Enums;

namespace TicketingService.Core.Interfaces.Services;

public interface ITicketService
{
    Task<TicketReadDto> CreateAsync(CreateTicketDto dto, string createdByUserId);
    Task<TicketReadDto> UpdateAsync(string id, UpdateTicketDto dto, string updatedByUserId);
    Task<TicketReadDto> ChangeStatusAsync(string id, ChangeStatusDto dto, string changedByUserId);
    Task<TicketReadDto> AssignAsync(string id, AssignTicketDto dto, string assignedByUserId);
    /// D2-5 — the ticket's assignment/ownership trail, most-recent first.
    Task<IEnumerable<TicketAssignmentReadDto>> GetAssignmentsAsync(string ticketId);

    // D3-1 — parent/child linking (distinct from dup-merge).
    Task<TicketReadDto> LinkChildAsync(string parentId, string childTicketId, string actor);
    Task<TicketReadDto> UnlinkParentAsync(string childTicketId, string actor);
    Task<IEnumerable<TicketReadDto>> GetChildrenAsync(string parentId);
    Task<TicketReadDto> AssignToDepartmentAsync(string id, AssignToDepartmentDto dto, string assignedByUserId);
    Task<TicketReadDto> ResolveAsync(string id, ResolveTicketDto dto, string resolvedByUserId);
    Task<TicketReadDto> CloseAsync(string id, string closedByUserId);
    Task<TicketReadDto> ReopenAsync(string id, string reopenedByUserId);
    Task<TicketReadDto> EscalateAsync(string id, EscalateTicketDto dto, string escalatedByUserId);
    Task<TicketReadDto?> GetByIdAsync(string id);
    Task<TicketReadDto?> GetByReferenceAsync(string reference);
    Task<PaginatedResult<TicketReadDto>> GetPagedAsync(TicketFilterParameters parameters, string? currentUserId = null);
    Task<CommentReadDto> AddCommentAsync(string ticketId, CreateCommentDto dto, string authorUserId);
    Task<IEnumerable<CommentReadDto>> GetCommentsAsync(string ticketId);
    /// #1 — public portal conversation (non-internal comments) for a ticket resolved by reference.
    Task<IEnumerable<CommentReadDto>> GetPublicConversationByReferenceAsync(string reference);
    /// #1 — a customer reply from the public tracking page: adds a public comment and un-pends the
    /// ticket (it's no longer waiting on the customer), stopping the no-response auto-close.
    Task<(bool Found, CommentReadDto? Comment)> AddPublicReplyAsync(string reference, string message, string? authorName);
    /// #19 — likely duplicates of a ticket (same requester, still open).
    Task<IEnumerable<TicketReadDto>> FindDuplicatesAsync(string id);
    /// #19 — merge a duplicate into a surviving ticket: link + close the source as a duplicate.
    Task<TicketReadDto> MergeAsync(string sourceId, string targetId, string mergedByUserId);
    Task AddWatcherAsync(string ticketId, string userId, string addedByUserId);
    Task RemoveWatcherAsync(string ticketId, string userId);
    Task<IEnumerable<string>> GetWatcherIdsAsync(string ticketId);
    Task<IEnumerable<TicketReadDto>> GetMyTicketsAsync(string userId);
    Task<IEnumerable<TicketReadDto>> GetCreatedByMeAsync(string userId);
    Task<object> GetDashboardSummaryAsync(string? departmentId = null);
    Task<object> GetMySummaryAsync(string userId);
    Task<(bool Found, string NewStatus)> ApplyWorkUpdateAsync(string ticketId, WorkUpdateType updateType, string source, string? notes);
}
