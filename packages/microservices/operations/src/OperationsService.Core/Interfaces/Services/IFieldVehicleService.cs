using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Vehicles;

namespace OperationsService.Core.Interfaces.Services;

public interface IFieldVehicleService
{
    // Field Vehicle registry
    Task<PaginatedResult<FieldVehicleReadDto>> GetVehiclesAsync(FieldVehicleFilterParameters filters);
    Task<FieldVehicleReadDto?> GetVehicleByIdAsync(string id);
    Task<FieldVehicleReadDto> CreateVehicleAsync(CreateFieldVehicleDto dto, string userId);
    Task<FieldVehicleReadDto> UpdateVehicleAsync(string id, UpdateFieldVehicleDto dto, string userId);
    Task DeleteVehicleAsync(string id, string userId);

    // Dispatch operations
    Task<IEnumerable<VehicleDispatchReadDto>> GetDispatchesByAssignmentAsync(string assignmentId);
    Task<IEnumerable<VehicleDispatchReadDto>> GetDispatchesByVehicleAsync(string vehicleId);
    Task<VehicleDispatchReadDto> CreateDispatchAsync(CreateVehicleDispatchDto dto, string userId);
    Task<VehicleDispatchReadDto> ApproveDispatchAsync(string dispatchId, string approvedBy, string userId);
    Task<VehicleDispatchReadDto> RejectDispatchAsync(string dispatchId, string userId);
    Task<VehicleDispatchReadDto> LogReturnAsync(string dispatchId, LogReturnDto dto, string userId);
    Task<VehicleDispatchReadDto> CancelDispatchAsync(string dispatchId, string userId);

    // Fuel logs
    Task<IEnumerable<FuelLogReadDto>> GetFuelLogsAsync(string dispatchId);
    Task<FuelLogReadDto> AddFuelLogAsync(CreateFuelLogDto dto, string userId);
}
