using OperationsService.Core.DTOs.Attachments;

namespace OperationsService.Core.Interfaces.Services;

public interface IAttachmentService
{
    Task<IEnumerable<AttachmentReadDto>> GetByEntityAsync(string entityType, string entityId);
    Task<IEnumerable<AttachmentReadDto>> GetByAssignmentAsync(string assignmentId);
    Task<AttachmentReadDto> UploadAsync(UploadAttachmentDto dto, string fileName, string contentType, long fileSizeBytes, string url, string userId, string userName);
    Task DeleteAsync(string attachmentId, string userId);
}
