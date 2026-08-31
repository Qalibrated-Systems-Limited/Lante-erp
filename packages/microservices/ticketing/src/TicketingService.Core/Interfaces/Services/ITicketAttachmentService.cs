using Microsoft.AspNetCore.Http;
using TicketingService.Core.DTOs.Tickets;

namespace TicketingService.Core.Interfaces.Services;

public interface ITicketAttachmentService
{
    Task<IEnumerable<TicketAttachmentReadDto>> GetByTicketIdAsync(string ticketId);
    Task<IEnumerable<TicketAttachmentReadDto>> AddAttachmentsAsync(string ticketId, IFormFileCollection files, string uploadedByUserId);
    Task<bool> DeleteAttachmentAsync(string attachmentId);
}
