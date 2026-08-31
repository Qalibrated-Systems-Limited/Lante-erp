namespace OperationsService.Core.Entities;

public class Attachment : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? AssignmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string StorageUrl { get; set; } = string.Empty;
    public string UploadedByUserId { get; set; } = string.Empty;
    public string? Description { get; set; }
}
