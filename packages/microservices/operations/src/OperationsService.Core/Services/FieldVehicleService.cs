using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Vehicles;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

public class FieldVehicleService : IFieldVehicleService
{
    private readonly IGenericRepository<FieldVehicle> _vehicles;
    private readonly IGenericRepository<VehicleDispatch> _dispatches;
    private readonly IGenericRepository<FuelLog> _fuelLogs;
    private readonly IGenericRepository<Assignment> _assignments;

    public FieldVehicleService(
        IGenericRepository<FieldVehicle> vehicles,
        IGenericRepository<VehicleDispatch> dispatches,
        IGenericRepository<FuelLog> fuelLogs,
        IGenericRepository<Assignment> assignments)
    {
        _vehicles = vehicles;
        _dispatches = dispatches;
        _fuelLogs = fuelLogs;
        _assignments = assignments;
    }

    // ─── Field Vehicle ────────────────────────────────────────────────────────

    public async Task<PaginatedResult<FieldVehicleReadDto>> GetVehiclesAsync(FieldVehicleFilterParameters filters)
    {
        var query = _vehicles.Query().Where(v => !v.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var s = filters.Search.ToLower();
            query = query.Where(v =>
                v.RegistrationNumber.ToLower().Contains(s) ||
                v.Make.ToLower().Contains(s) ||
                v.Model.ToLower().Contains(s));
        }

        if (filters.Status.HasValue)
            query = query.Where(v => v.Status == filters.Status.Value);

        if (filters.Type.HasValue)
            query = query.Where(v => v.Type == filters.Type.Value);

        query = query.OrderBy(v => v.RegistrationNumber);

        var total = await query.CountAsync();
        var items = await query
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToListAsync();

        return new PaginatedResult<FieldVehicleReadDto>
        {
            Items = items.Select(MapVehicle).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize
        };
    }

    public async Task<FieldVehicleReadDto?> GetVehicleByIdAsync(string id)
    {
        var v = await _vehicles.Query().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
        return v is null ? null : MapVehicle(v);
    }

    public async Task<FieldVehicleReadDto> CreateVehicleAsync(CreateFieldVehicleDto dto, string userId)
    {
        var vehicle = new FieldVehicle
        {
            RegistrationNumber = dto.RegistrationNumber,
            Make = dto.Make,
            Model = dto.Model,
            Year = dto.Year,
            Type = dto.Type,
            Color = dto.Color,
            CurrentOdometer = dto.CurrentOdometer,
            LastServiceDate = dto.LastServiceDate,
            NextServiceDate = dto.NextServiceDate,
            LastServiceOdometer = dto.LastServiceOdometer,
            InsuranceExpiry = dto.InsuranceExpiry,
            InspectionExpiryDate = dto.InspectionExpiryDate,
            Notes = dto.Notes,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        await _vehicles.CreateAsync(vehicle);
        return MapVehicle(vehicle);
    }

    public async Task<FieldVehicleReadDto> UpdateVehicleAsync(string id, UpdateFieldVehicleDto dto, string userId)
    {
        var vehicle = await _vehicles.Query().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted)
            ?? throw new KeyNotFoundException($"Field vehicle {id} not found.");

        if (dto.RegistrationNumber is not null) vehicle.RegistrationNumber = dto.RegistrationNumber;
        if (dto.Make is not null) vehicle.Make = dto.Make;
        if (dto.Model is not null) vehicle.Model = dto.Model;
        if (dto.Year.HasValue) vehicle.Year = dto.Year.Value;
        if (dto.Type.HasValue) vehicle.Type = dto.Type.Value;
        if (dto.Color is not null) vehicle.Color = dto.Color;
        if (dto.CurrentOdometer.HasValue) vehicle.CurrentOdometer = dto.CurrentOdometer.Value;
        if (dto.Status.HasValue) vehicle.Status = dto.Status.Value;
        if (dto.LastServiceDate.HasValue) vehicle.LastServiceDate = dto.LastServiceDate;
        if (dto.NextServiceDate.HasValue) vehicle.NextServiceDate = dto.NextServiceDate;
        if (dto.LastServiceOdometer.HasValue) vehicle.LastServiceOdometer = dto.LastServiceOdometer;
        if (dto.InsuranceExpiry is not null) vehicle.InsuranceExpiry = dto.InsuranceExpiry;
        if (dto.InspectionExpiryDate.HasValue) vehicle.InspectionExpiryDate = dto.InspectionExpiryDate;
        if (dto.Notes is not null) vehicle.Notes = dto.Notes;

        vehicle.UpdatedBy = userId;
        vehicle.UpdatedAt = DateTime.UtcNow;
        await _vehicles.UpdateAsync(vehicle);
        return MapVehicle(vehicle);
    }

    public async Task DeleteVehicleAsync(string id, string userId)
    {
        var vehicle = await _vehicles.Query().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted)
            ?? throw new KeyNotFoundException($"Field vehicle {id} not found.");

        vehicle.IsDeleted = true;
        vehicle.UpdatedBy = userId;
        vehicle.UpdatedAt = DateTime.UtcNow;
        await _vehicles.UpdateAsync(vehicle);
    }

    // ─── Dispatch ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<VehicleDispatchReadDto>> GetDispatchesByAssignmentAsync(string assignmentId)
    {
        var dispatches = await _dispatches.Query()
            .Include(d => d.FieldVehicle)
            .Include(d => d.FuelLogs)
            .Where(d => d.AssignmentId == assignmentId && !d.IsDeleted)
            .OrderByDescending(d => d.DepartureDatetime)
            .ToListAsync();

        var assignmentIds = dispatches.Select(d => d.AssignmentId).Distinct().ToList();
        var assignments = await _assignments.Query()
            .Where(a => assignmentIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Title })
            .ToListAsync();
        var titleMap = assignments.ToDictionary(a => a.Id, a => a.Title);

        return dispatches.Select(d => MapDispatch(d, titleMap.GetValueOrDefault(d.AssignmentId, string.Empty)));
    }

    public async Task<IEnumerable<VehicleDispatchReadDto>> GetDispatchesByVehicleAsync(string vehicleId)
    {
        var dispatches = await _dispatches.Query()
            .Include(d => d.FieldVehicle)
            .Include(d => d.FuelLogs)
            .Where(d => d.FieldVehicleId == vehicleId && !d.IsDeleted)
            .OrderByDescending(d => d.DepartureDatetime)
            .ToListAsync();

        var assignmentIds = dispatches.Select(d => d.AssignmentId).Distinct().ToList();
        var assignments = await _assignments.Query()
            .Where(a => assignmentIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Title })
            .ToListAsync();
        var titleMap = assignments.ToDictionary(a => a.Id, a => a.Title);

        return dispatches.Select(d => MapDispatch(d, titleMap.GetValueOrDefault(d.AssignmentId, string.Empty)));
    }

    public async Task<VehicleDispatchReadDto> CreateDispatchAsync(CreateVehicleDispatchDto dto, string userId)
    {
        var vehicle = await _vehicles.Query().FirstOrDefaultAsync(v => v.Id == dto.FieldVehicleId && !v.IsDeleted)
            ?? throw new KeyNotFoundException($"Field vehicle {dto.FieldVehicleId} not found.");

        if (await HasOpenDispatchAsync(dto.FieldVehicleId, excludeDispatchId: null))
            throw new InvalidOperationException("This vehicle already has a pending or active dispatch.");

        // Starts Pending — the vehicle isn't marked unavailable and its odometer isn't
        // updated until a fleet manager approves the request (see ApproveDispatchAsync).
        var dispatch = new VehicleDispatch
        {
            AssignmentId = dto.AssignmentId,
            FieldVehicleId = dto.FieldVehicleId,
            DriverName = dto.DriverName,
            DepartureDatetime = dto.DepartureDatetime,
            DepartureOdometer = dto.DepartureOdometer,
            FuelLevelOut = dto.FuelLevelOut,
            Notes = dto.Notes,
            Status = VehicleDispatchStatus.Pending,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        await _dispatches.CreateAsync(dispatch);

        var created = await _dispatches.Query()
            .Include(d => d.FieldVehicle)
            .Include(d => d.FuelLogs)
            .FirstAsync(d => d.Id == dispatch.Id);

        var assignment = await _assignments.Query()
            .Where(a => a.Id == dto.AssignmentId)
            .Select(a => new { a.Id, a.Title })
            .FirstOrDefaultAsync();

        return MapDispatch(created, assignment?.Title ?? string.Empty);
    }

    public async Task<VehicleDispatchReadDto> ApproveDispatchAsync(string dispatchId, string approvedBy, string userId)
    {
        var dispatch = await _dispatches.Query()
            .Include(d => d.FieldVehicle)
            .Include(d => d.FuelLogs)
            .FirstOrDefaultAsync(d => d.Id == dispatchId && !d.IsDeleted)
            ?? throw new KeyNotFoundException($"Dispatch {dispatchId} not found.");

        if (dispatch.Status != VehicleDispatchStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be approved.");

        // Re-check — another request may have been approved for this vehicle since this one was made.
        if (await HasOpenDispatchAsync(dispatch.FieldVehicleId, excludeDispatchId: dispatch.Id))
            throw new InvalidOperationException("This vehicle now has another pending or active dispatch.");

        dispatch.Status = VehicleDispatchStatus.Dispatched;
        dispatch.ApprovedBy = approvedBy;
        dispatch.ApprovedAt = DateTime.UtcNow;
        dispatch.UpdatedBy = userId;
        dispatch.UpdatedAt = DateTime.UtcNow;

        if (dispatch.FieldVehicle is not null)
        {
            dispatch.FieldVehicle.CurrentOdometer = dispatch.DepartureOdometer;
            dispatch.FieldVehicle.Status = FieldVehicleStatus.Dispatched;
            dispatch.FieldVehicle.UpdatedBy = userId;
            dispatch.FieldVehicle.UpdatedAt = DateTime.UtcNow;
            await _vehicles.UpdateAsync(dispatch.FieldVehicle);
        }

        await _dispatches.UpdateAsync(dispatch);

        var assignment = await _assignments.Query()
            .Where(a => a.Id == dispatch.AssignmentId)
            .Select(a => new { a.Id, a.Title })
            .FirstOrDefaultAsync();

        return MapDispatch(dispatch, assignment?.Title ?? string.Empty);
    }

    public async Task<VehicleDispatchReadDto> RejectDispatchAsync(string dispatchId, string userId)
    {
        var dispatch = await _dispatches.Query()
            .Include(d => d.FieldVehicle)
            .Include(d => d.FuelLogs)
            .FirstOrDefaultAsync(d => d.Id == dispatchId && !d.IsDeleted)
            ?? throw new KeyNotFoundException($"Dispatch {dispatchId} not found.");

        if (dispatch.Status != VehicleDispatchStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be rejected.");

        dispatch.Status = VehicleDispatchStatus.Rejected;
        dispatch.UpdatedBy = userId;
        dispatch.UpdatedAt = DateTime.UtcNow;
        await _dispatches.UpdateAsync(dispatch);

        var assignment = await _assignments.Query()
            .Where(a => a.Id == dispatch.AssignmentId)
            .Select(a => new { a.Id, a.Title })
            .FirstOrDefaultAsync();

        return MapDispatch(dispatch, assignment?.Title ?? string.Empty);
    }

    private async Task<bool> HasOpenDispatchAsync(string fieldVehicleId, string? excludeDispatchId)
    {
        var open = await _dispatches.Query()
            .Where(d => d.FieldVehicleId == fieldVehicleId && !d.IsDeleted &&
                        (d.Status == VehicleDispatchStatus.Pending || d.Status == VehicleDispatchStatus.Dispatched))
            .ToListAsync();

        return open.Any(d => d.Id != excludeDispatchId);
    }

    public async Task<VehicleDispatchReadDto> LogReturnAsync(string dispatchId, LogReturnDto dto, string userId)
    {
        var dispatch = await _dispatches.Query()
            .Include(d => d.FieldVehicle)
            .Include(d => d.FuelLogs)
            .FirstOrDefaultAsync(d => d.Id == dispatchId && !d.IsDeleted)
            ?? throw new KeyNotFoundException($"Dispatch {dispatchId} not found.");

        if (dispatch.Status != VehicleDispatchStatus.Dispatched)
            throw new InvalidOperationException("Can only log return on an active dispatch.");

        dispatch.ReturnDatetime = dto.ReturnDatetime;
        dispatch.ReturnOdometer = dto.ReturnOdometer;
        dispatch.FuelLevelIn = dto.FuelLevelIn;
        if (dto.Notes is not null) dispatch.Notes = dto.Notes;
        dispatch.Status = VehicleDispatchStatus.Returned;
        dispatch.UpdatedBy = userId;
        dispatch.UpdatedAt = DateTime.UtcNow;

        if (dispatch.FieldVehicle is not null)
        {
            dispatch.FieldVehicle.CurrentOdometer = dto.ReturnOdometer;
            dispatch.FieldVehicle.Status = FieldVehicleStatus.Available;
            dispatch.FieldVehicle.UpdatedBy = userId;
            dispatch.FieldVehicle.UpdatedAt = DateTime.UtcNow;
            await _vehicles.UpdateAsync(dispatch.FieldVehicle);
        }

        await _dispatches.UpdateAsync(dispatch);

        var assignment = await _assignments.Query()
            .Where(a => a.Id == dispatch.AssignmentId)
            .Select(a => new { a.Id, a.Title })
            .FirstOrDefaultAsync();

        return MapDispatch(dispatch, assignment?.Title ?? string.Empty);
    }

    public async Task<VehicleDispatchReadDto> CancelDispatchAsync(string dispatchId, string userId)
    {
        var dispatch = await _dispatches.Query()
            .Include(d => d.FieldVehicle)
            .Include(d => d.FuelLogs)
            .FirstOrDefaultAsync(d => d.Id == dispatchId && !d.IsDeleted)
            ?? throw new KeyNotFoundException($"Dispatch {dispatchId} not found.");

        if (dispatch.Status is not (VehicleDispatchStatus.Pending or VehicleDispatchStatus.Dispatched))
            throw new InvalidOperationException("Only pending or active dispatches can be cancelled.");

        var wasDispatched = dispatch.Status == VehicleDispatchStatus.Dispatched;
        dispatch.Status = VehicleDispatchStatus.Cancelled;
        dispatch.UpdatedBy = userId;
        dispatch.UpdatedAt = DateTime.UtcNow;

        // A Pending request never marked the vehicle unavailable, so only free it here
        // if this cancellation is actually ending an active (Dispatched) use.
        if (wasDispatched && dispatch.FieldVehicle is not null)
        {
            dispatch.FieldVehicle.Status = FieldVehicleStatus.Available;
            dispatch.FieldVehicle.UpdatedBy = userId;
            dispatch.FieldVehicle.UpdatedAt = DateTime.UtcNow;
            await _vehicles.UpdateAsync(dispatch.FieldVehicle);
        }

        await _dispatches.UpdateAsync(dispatch);

        var assignment = await _assignments.Query()
            .Where(a => a.Id == dispatch.AssignmentId)
            .Select(a => new { a.Id, a.Title })
            .FirstOrDefaultAsync();

        return MapDispatch(dispatch, assignment?.Title ?? string.Empty);
    }

    // ─── Fuel Logs ────────────────────────────────────────────────────────────

    public async Task<IEnumerable<FuelLogReadDto>> GetFuelLogsAsync(string dispatchId)
    {
        var logs = await _fuelLogs.Query()
            .Where(f => f.DispatchId == dispatchId && !f.IsDeleted)
            .OrderByDescending(f => f.LoggedAt)
            .ToListAsync();

        return logs.Select(MapFuelLog);
    }

    public async Task<FuelLogReadDto> AddFuelLogAsync(CreateFuelLogDto dto, string userId)
    {
        _ = await _dispatches.Query().FirstOrDefaultAsync(d => d.Id == dto.DispatchId && !d.IsDeleted)
            ?? throw new KeyNotFoundException($"Dispatch {dto.DispatchId} not found.");

        var log = new FuelLog
        {
            DispatchId = dto.DispatchId,
            AmountLitres = dto.AmountLitres,
            CostKes = dto.CostKes,
            Location = dto.Location,
            LoggedAt = dto.LoggedAt ?? DateTime.UtcNow,
            Notes = dto.Notes,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        await _fuelLogs.CreateAsync(log);
        return MapFuelLog(log);
    }

    // ─── Mappers ──────────────────────────────────────────────────────────────

    private static FieldVehicleReadDto MapVehicle(FieldVehicle v) => new()
    {
        Id = v.Id,
        RegistrationNumber = v.RegistrationNumber,
        Make = v.Make,
        Model = v.Model,
        Year = v.Year,
        Type = v.Type.ToString(),
        Color = v.Color,
        CurrentOdometer = v.CurrentOdometer,
        Status = v.Status.ToString(),
        LastServiceDate = v.LastServiceDate,
        NextServiceDate = v.NextServiceDate,
        LastServiceOdometer = v.LastServiceOdometer,
        InsuranceExpiry = v.InsuranceExpiry,
        InspectionExpiryDate = v.InspectionExpiryDate,
        Notes = v.Notes,
        CreatedAt = v.CreatedAt,
        UpdatedAt = v.UpdatedAt
    };

    private static VehicleDispatchReadDto MapDispatch(VehicleDispatch d, string assignmentTitle) => new()
    {
        Id = d.Id,
        AssignmentId = d.AssignmentId,
        AssignmentTitle = assignmentTitle,
        FieldVehicleId = d.FieldVehicleId,
        VehicleRegistration = d.FieldVehicle?.RegistrationNumber ?? string.Empty,
        VehicleMake = d.FieldVehicle?.Make ?? string.Empty,
        VehicleModel = d.FieldVehicle?.Model ?? string.Empty,
        VehicleType = d.FieldVehicle?.Type.ToString() ?? string.Empty,
        DriverName = d.DriverName,
        DepartureDatetime = d.DepartureDatetime,
        DepartureOdometer = d.DepartureOdometer,
        FuelLevelOut = d.FuelLevelOut,
        ReturnDatetime = d.ReturnDatetime,
        ReturnOdometer = d.ReturnOdometer,
        FuelLevelIn = d.FuelLevelIn,
        Notes = d.Notes,
        Status = d.Status.ToString(),
        ApprovedBy = d.ApprovedBy,
        ApprovedAt = d.ApprovedAt,
        CreatedAt = d.CreatedAt,
        FuelLogs = d.FuelLogs?.Where(f => !f.IsDeleted).Select(MapFuelLog).ToList() ?? []
    };

    private static FuelLogReadDto MapFuelLog(FuelLog f) => new()
    {
        Id = f.Id,
        DispatchId = f.DispatchId,
        AmountLitres = f.AmountLitres,
        CostKes = f.CostKes,
        Location = f.Location,
        LoggedAt = f.LoggedAt,
        CreatedBy = f.CreatedBy,
        Notes = f.Notes
    };
}
