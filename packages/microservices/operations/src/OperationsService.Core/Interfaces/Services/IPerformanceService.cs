using OperationsService.Core.DTOs.Performance;

namespace OperationsService.Core.Interfaces.Services;

public interface IPerformanceService
{
    Task<PerformanceMetricsReadDto?> GetMetricsAsync(string technicianId, int month, int year);
    Task<PerformanceSummaryDto> GetDepartmentSummaryAsync(string departmentId, int month, int year);
    Task ComputeMetricsAsync(string technicianId, int month, int year);
    Task ComputeDepartmentMetricsAsync(string departmentId, int month, int year);
}
