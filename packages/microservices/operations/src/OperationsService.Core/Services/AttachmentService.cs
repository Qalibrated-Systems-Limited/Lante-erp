using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Attachments;
using OperationsService.Core.Entities;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

public class AttachmentService : IAttachmentService
{
    private readonly IGenericRepository<Attachment> _attachments;
    private readonly IGenericRepository<ServiceReport> _serviceReports;
    private readonly IMapper _mapper;

    public AttachmentService(
        IGenericRepository<Attachment> attachments,
        IGenericRepository<ServiceReport> serviceReports,
        IMapper mapper)
    {
        _attachments = attachments;
        _serviceReports = serviceReports;
        _mapper = mapper;
    }

    public async Task<IEnumerable<AttachmentReadDto>> GetByEntityAsync(string entityType, string entityId)
    {
        var items = await _attachments.Query()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return _mapper.Map<List<AttachmentReadDto>>(items);
    }

    public async Task<IEnumerable<AttachmentReadDto>> GetByAssignmentAsync(string assignmentId)
    {
        // Gather entity IDs belonging to this assignment for each type, so we catch
        // attachments uploaded before AssignmentId was tracked on the Attachment row.
        var srIds = await _serviceReports.Query()
            .Where(sr => sr.AssignmentId == assignmentId && !sr.IsDeleted)
            .Select(sr => sr.Id)
            .ToListAsync();

        var items = await _attachments.Query()
            .Where(a => !a.IsDeleted && (
                a.AssignmentId == assignmentId ||
                (a.EntityType == "ServiceReport" && srIds.Contains(a.EntityId))
            ))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return _mapper.Map<List<AttachmentReadDto>>(items);
    }

    public async Task<AttachmentReadDto> UploadAsync(UploadAttachmentDto dto, string fileName, string contentType, long fileSizeBytes, string url, string userId, string userName)
    {
        var attachment = new Attachment
        {
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            AssignmentId = dto.AssignmentId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            StorageUrl = url,
            Description = dto.Description,
            UploadedByUserId = userId,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        var created = await _attachments.CreateAsync(attachment);
        var readDto = _mapper.Map<AttachmentReadDto>(created);
        readDto.UploadedByName = userName;
        return readDto;
    }

    public async Task DeleteAsync(string attachmentId, string userId)
    {
        var attachment = await _attachments.GetByIdAsync(attachmentId)
            ?? throw new KeyNotFoundException("Attachment not found.");
        attachment.IsDeleted = true;
        attachment.UpdatedBy = userId;
        attachment.UpdatedAt = DateTime.UtcNow;
        await _attachments.UpdateAsync(attachment);
    }
}
