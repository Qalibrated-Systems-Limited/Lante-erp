using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using TicketingService.Core.DTOs.Tickets;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

public class TicketAttachmentService(
    ITicketAttachmentRepository attachmentRepository,
    ITicketRepository ticketRepository,
    IConfiguration configuration,
    IMapper mapper) : ITicketAttachmentService
{
    private const int MaxPhotosPerTicket = 3;
    private const long MaxFileSizeBytes  = 5 * 1024 * 1024;
    private static readonly string[] AllowedMimeTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    private string BasePath =>
        configuration["Storage:BasePath"] ?? "/app/uploads";

    private string PublicBaseUrl
    {
        get
        {
            var url = configuration["Storage:BaseUrl"];
            return url is { } u && u.StartsWith("http") ? u.TrimEnd('/') + "/uploads" : "/uploads";
        }
    }

    public async Task<IEnumerable<TicketAttachmentReadDto>> GetByTicketIdAsync(string ticketId)
    {
        var attachments = await attachmentRepository.GetByTicketIdAsync(ticketId);
        return mapper.Map<IEnumerable<TicketAttachmentReadDto>>(attachments);
    }

    public async Task<IEnumerable<TicketAttachmentReadDto>> AddAttachmentsAsync(
        string ticketId, IFormFileCollection files, string uploadedByUserId)
    {
        _ = await ticketRepository.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} not found.");

        var existing = await attachmentRepository.CountByTicketIdAsync(ticketId);
        if (existing + files.Count > MaxPhotosPerTicket)
            throw new InvalidOperationException($"A ticket may have at most {MaxPhotosPerTicket} photos. {MaxPhotosPerTicket - existing} slot(s) remaining.");

        var uploadDir = Path.Combine(BasePath, "ticket-attachments", ticketId);
        Directory.CreateDirectory(uploadDir);

        var result = new List<TicketAttachmentReadDto>();

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            if (file.Length > MaxFileSizeBytes)
                throw new InvalidOperationException($"'{file.FileName}' exceeds the 5 MB limit.");
            if (!AllowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
                throw new InvalidOperationException($"'{file.FileName}' is not a supported image type (JPEG, PNG, WebP, GIF).");

            var ext      = Path.GetExtension(file.FileName).ToLowerInvariant();
            var safeName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadDir, safeName);

            await using (var stream = File.Create(filePath))
                await file.CopyToAsync(stream);

            var attachment = new TicketAttachment
            {
                TicketId         = ticketId,
                FileName         = file.FileName,
                FileUrl          = $"{PublicBaseUrl}/ticket-attachments/{ticketId}/{safeName}",
                FileSize         = file.Length,
                ContentType      = file.ContentType,
                UploadedByUserId = uploadedByUserId,
            };

            await attachmentRepository.CreateAsync(attachment);
            result.Add(mapper.Map<TicketAttachmentReadDto>(attachment));
        }

        return result;
    }

    public async Task<bool> DeleteAttachmentAsync(string attachmentId)
    {
        var attachment = await attachmentRepository.GetByIdAsync(attachmentId);
        if (attachment == null) return false;

        try
        {
            var uploadsMarker = "/uploads/";
            var idx = attachment.FileUrl.IndexOf(uploadsMarker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var relative = attachment.FileUrl[(idx + uploadsMarker.Length)..];
                var physicalPath = Path.Combine(BasePath, relative);
                if (File.Exists(physicalPath))
                    File.Delete(physicalPath);
            }
        }
        catch { /* file already gone — proceed with DB cleanup */ }

        return await attachmentRepository.DeleteAsync(attachmentId);
    }
}
