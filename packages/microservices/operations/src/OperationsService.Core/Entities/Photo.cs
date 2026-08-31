using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class Photo : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string UploadedByUserId { get; set; } = string.Empty;
    public PhotoType Type { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? StorageUrl { get; set; }
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Caption { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
