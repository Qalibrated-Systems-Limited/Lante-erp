using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Photos;

public class UploadPhotoDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public PhotoType PhotoType { get; set; }
    public string? Caption { get; set; }
}

public class PhotoReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string UploadedByUserId { get; set; } = string.Empty;
    public string UploadedByName { get; set; } = string.Empty;
    public string PhotoType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public DateTime UploadedAt { get; set; }
}
