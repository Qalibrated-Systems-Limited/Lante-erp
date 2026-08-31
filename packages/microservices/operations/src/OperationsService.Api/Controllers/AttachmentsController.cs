using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Attachments;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.Interfaces.Services;
using System.Security.Claims;

namespace OperationsService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/attachments")]
[Authorize]
public class AttachmentsController(IAttachmentService attachmentService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<AttachmentReadDto>>>> Get(
        [FromQuery] string entityType, [FromQuery] string entityId)
    {
        var items = await attachmentService.GetByEntityAsync(entityType, entityId);
        return Ok(new ApiResponse<IEnumerable<AttachmentReadDto>> { Success = true, Data = items });
    }

    [HttpGet("by-assignment/{assignmentId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AttachmentReadDto>>>> GetByAssignment(Guid assignmentId)
    {
        var items = await attachmentService.GetByAssignmentAsync(assignmentId.ToString());
        return Ok(new ApiResponse<IEnumerable<AttachmentReadDto>> { Success = true, Data = items });
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<AttachmentReadDto>>> Upload(
        [FromForm] UploadAttachmentDto dto, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ApiResponse<AttachmentReadDto> { Success = false, Message = "No file provided." });

        var uploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH")
                         ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsPath, "attachments", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var attachment = await attachmentService.UploadAsync(
            dto, file.FileName, file.ContentType, file.Length, $"/uploads/attachments/{fileName}", UserId, UserName);
        return Ok(new ApiResponse<AttachmentReadDto> { Success = true, Data = attachment });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id)
    {
        await attachmentService.DeleteAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Attachment deleted." });
    }
}
