using FleetService.Core.Entities;

namespace FleetService.Core.Interfaces;

public interface IFieldVehicleRepository : IRepository<FieldVehicle>
{
    Task<(IEnumerable<FieldVehicle> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, string? search = null, FieldVehicleStatus? status = null, FieldVehicleType? type = null);
}
