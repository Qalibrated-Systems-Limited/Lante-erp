using OperationsService.Core.DTOs.ServiceReports;

namespace OperationsService.Core.Interfaces.Services;

public interface IServiceReportService
{
    Task<ServiceReportReadDto?> GetByIdAsync(string id);
    Task<ServiceReportReadDto?> GetByAssignmentIdAsync(string assignmentId);
    Task<ServiceReportReadDto> CreateAsync(CreateServiceReportDto dto, string technicianId, string technicianName);
    Task<ServiceReportReadDto> UpdateAsync(string id, UpdateServiceReportDto dto, string userId);
    Task<ServiceReportReadDto> SignAsync(string id, SignServiceReportDto dto, string userId);
    Task<ServiceReportReadDto> ReviewAsync(string id, ReviewServiceReportDto dto, string reviewerId, string reviewerName);
    Task<IEnumerable<FsrEquipmentDto>> GetEquipmentHistoryAsync(string serialNumber);
}
