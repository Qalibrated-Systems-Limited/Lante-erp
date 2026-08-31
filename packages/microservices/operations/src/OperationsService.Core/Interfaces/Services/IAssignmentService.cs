using OperationsService.Core.DTOs.Assignments;
using OperationsService.Core.DTOs.CheckIns;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.DailySummaries;
using OperationsService.Core.DTOs.Photos;

namespace OperationsService.Core.Interfaces.Services;

public interface IAssignmentService
{
    Task<AssignmentReadDto?> GetByIdAsync(string id);
    Task<PaginatedResult<AssignmentReadDto>> GetAllAsync(AssignmentFilterParameters filters, string? departmentId);
    Task<AssignmentReadDto> CreateAsync(CreateAssignmentDto dto, string managerId);
    Task<AssignmentReadDto> UpdateAsync(string id, UpdateAssignmentDto dto, string userId);
    Task DeleteAsync(string id, string userId);
    Task<AssignmentReadDto> AcceptAsync(string id, string technicianId);
    Task<AssignmentReadDto> StartAsync(string id, string technicianId);
    Task<AssignmentReadDto> CompleteAsync(string id, string technicianId);
    Task<AssignmentReadDto> CancelAsync(string id, CancelAssignmentDto dto, string userId);
    Task<AssignmentReadDto> SubmitForLinkingAsync(string id, string userId);
    Task<AssignmentReadDto> LinkToProjectAsync(string id, LinkToProjectDto dto, string managerId, string managerName);
    Task<AssignmentReadDto> ArchiveStandaloneAsync(string id, ArchiveStandaloneDto dto, string userId);

    // Check-ins
    Task<CheckInReadDto> CheckInAsync(CheckInDto dto, string userId, string userName);
    /// <summary>PR2 — also captures the elapsed time onto the technician's weekly timesheet.</summary>
    Task<CheckInReadDto> CheckOutAsync(CheckOutDto dto, string userId, string userName);
    Task<IEnumerable<CheckInReadDto>> GetCheckInsAsync(string assignmentId);

    // Photos
    Task<PhotoReadDto> UploadPhotoAsync(UploadPhotoDto dto, string fileUrl, string userId, string userName);
    Task<IEnumerable<PhotoReadDto>> GetPhotosAsync(string assignmentId);
    Task DeletePhotoAsync(string photoId, string userId);

    // Daily summaries
    Task<DailySummaryReadDto> CreateDailySummaryAsync(CreateDailySummaryDto dto, string userId, string userName);
    Task<DailySummaryReadDto> UpdateDailySummaryAsync(string summaryId, UpdateDailySummaryDto dto, string userId);
    Task<IEnumerable<DailySummaryReadDto>> GetDailySummariesAsync(string assignmentId);
}
