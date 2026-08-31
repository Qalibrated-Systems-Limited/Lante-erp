using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using FleetService.Core.DTOs.FieldVehicle;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface IFieldVehicleService
{
    Task<IEnumerable<FieldVehicleResponseDto>> GetVehiclesAsync(FieldVehicleFilterParameters filters);
    Task<(IEnumerable<FieldVehicleResponseDto> Items, int TotalCount)> GetPagedVehiclesAsync(int pageNumber, int pageSize, FieldVehicleFilterParameters filters);
    Task<FieldVehicleResponseDto?> GetVehicleByIdAsync(string id);
    Task<FieldVehicleResponseDto> CreateVehicleAsync(CreateFieldVehicleDto dto);
    Task<FieldVehicleResponseDto> UpdateVehicleAsync(string id, UpdateFieldVehicleDto dto);
    Task DeleteVehicleAsync(string id);

    Task<IEnumerable<VehicleDispatchResponseDto>> GetDispatchesByAssignmentAsync(string assignmentId);
    Task<IEnumerable<VehicleDispatchResponseDto>> GetDispatchesByVehicleAsync(string fieldVehicleId);
    Task<int> GetPendingDispatchCountAsync();
    Task<IEnumerable<VehicleDispatchResponseDto>> GetPendingDispatchesAsync();
    Task<VehicleDispatchResponseDto> RequestDispatchAsync(CreateVehicleDispatchDto dto, string? requestedByUserId = null);
    Task<VehicleDispatchResponseDto> ApproveDispatchAsync(string dispatchId, string approvedBy, string? approvedByUserId = null);
    Task<VehicleDispatchResponseDto> RejectDispatchAsync(string dispatchId);
    Task<VehicleDispatchResponseDto> LogReturnAsync(string dispatchId, LogReturnDto dto);
    Task<VehicleDispatchResponseDto> CancelDispatchAsync(string dispatchId);

    Task<IEnumerable<DispatchFuelLogResponseDto>> GetDispatchFuelLogsAsync(string dispatchId);
    Task<DispatchFuelLogResponseDto> AddDispatchFuelLogAsync(string dispatchId, CreateDispatchFuelLogDto dto);
}

public class FieldVehicleService(
    IFieldVehicleRepository vehicles,
    IRepository<VehicleDispatch> dispatches,
    IRepository<DispatchFuelLog> fuelLogs,
    ITicketingServiceClient ticketingClient,
    IUserServiceClient userServiceClient,
    IOperationsServiceClient operationsServiceClient,
    IHttpContextAccessor httpContextAccessor,
    ILogger<FieldVehicleService> logger) : IFieldVehicleService
{
    // Mirrors TenantDbConnectionInterceptor.Resolve() — the current request's own tenant schema,
    // needed because CreateAlertAsync is a service-to-service call with no schema claim of its own.
    private string? CurrentTenantSchema()
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx == null) return null;
        return ctx.User.FindFirst("schema")?.Value ?? ctx.Request.Headers["X-Tenant-Schema"].FirstOrDefault();
    }

    // Same formats the AddFieldVehicleInsuranceExpiryDate migration's backfill accepts (#375),
    // so a value that reads fine going forward parses the same way a legacy row would have.
    // InsuranceExpiry stays a free-text DTO field (the frontend's <input type="date"> already
    // emits ISO, but nothing stops a direct API caller from sending something else) — this is
    // what keeps InsuranceExpiryDate, the field VehicleExpiryBackgroundService actually reads,
    // populated for new writes instead of only ever being backfilled once by that migration.
    private static readonly string[] InsuranceExpiryFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "dd.MM.yyyy"];

    internal static DateTime? TryParseInsuranceExpiry(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTime.TryParseExact(raw.Trim(), InsuranceExpiryFormats,
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    // ─── Field Vehicle registry ─────────────────────────────────────────────

    public async Task<IEnumerable<FieldVehicleResponseDto>> GetVehiclesAsync(FieldVehicleFilterParameters filters)
    {
        var all = await vehicles.GetAllAsync();
        var query = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var s = filters.Search.ToLower();
            query = query.Where(v =>
                v.RegistrationNumber.ToLower().Contains(s) ||
                v.Make.ToLower().Contains(s) ||
                v.Model.ToLower().Contains(s));
        }
        if (filters.Status.HasValue) query = query.Where(v => v.Status == filters.Status.Value);
        if (filters.Type.HasValue) query = query.Where(v => v.Type == filters.Type.Value);

        if (filters.ExcludeOpenDispatch)
        {
            var openVehicleIds = (await dispatches.FindAsync(d =>
                    d.Status == VehicleDispatchStatus.Pending || d.Status == VehicleDispatchStatus.Dispatched))
                .Select(d => d.FieldVehicleId)
                .ToHashSet();
            query = query.Where(v => !openVehicleIds.Contains(v.Id));
        }

        var page = query.OrderBy(v => v.RegistrationNumber)
            .Skip(((filters.Page ?? 1) - 1) * filters.PageSize)
            .Take(filters.PageSize);

        return page.Select(MapVehicle);
    }

    public async Task<(IEnumerable<FieldVehicleResponseDto> Items, int TotalCount)> GetPagedVehiclesAsync(
        int pageNumber, int pageSize, FieldVehicleFilterParameters filters)
    {
        var (items, total) = await vehicles.GetPagedAsync(pageNumber, pageSize, filters.Search, filters.Status, filters.Type);
        return (items.Select(MapVehicle), total);
    }

    public async Task<FieldVehicleResponseDto?> GetVehicleByIdAsync(string id)
    {
        var v = await vehicles.GetByIdAsync(id);
        return v == null ? null : MapVehicle(v);
    }

    public async Task<FieldVehicleResponseDto> CreateVehicleAsync(CreateFieldVehicleDto dto)
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
            ServiceIntervalKm = dto.ServiceIntervalKm,
            InsuranceExpiry = dto.InsuranceExpiry,
            InsuranceExpiryDate = TryParseInsuranceExpiry(dto.InsuranceExpiry),
            InspectionExpiryDate = dto.InspectionExpiryDate,
            Notes = dto.Notes,
        };
        var created = await vehicles.CreateAsync(vehicle);
        return MapVehicle(created);
    }

    public async Task<FieldVehicleResponseDto> UpdateVehicleAsync(string id, UpdateFieldVehicleDto dto)
    {
        var vehicle = await vehicles.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Field vehicle {id} not found.");

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
        if (dto.ServiceIntervalKm.HasValue) vehicle.ServiceIntervalKm = dto.ServiceIntervalKm;
        if (dto.InsuranceExpiry is not null)
        {
            vehicle.InsuranceExpiry = dto.InsuranceExpiry;
            vehicle.InsuranceExpiryDate = TryParseInsuranceExpiry(dto.InsuranceExpiry);
        }
        if (dto.InspectionExpiryDate.HasValue) vehicle.InspectionExpiryDate = dto.InspectionExpiryDate;
        if (dto.Notes is not null) vehicle.Notes = dto.Notes;

        var updated = await vehicles.UpdateAsync(vehicle);
        return MapVehicle(updated!);
    }

    public async Task DeleteVehicleAsync(string id)
    {
        var deleted = await vehicles.DeleteAsync(id);
        if (!deleted) throw new KeyNotFoundException($"Field vehicle {id} not found.");
    }

    // ─── Dispatch ────────────────────────────────────────────────────────────

    public async Task<IEnumerable<VehicleDispatchResponseDto>> GetDispatchesByAssignmentAsync(string assignmentId)
    {
        var results = await dispatches.FindAsync(d => d.AssignmentId == assignmentId);
        return await MapDispatchesAsync(results.OrderByDescending(d => d.DepartureDatetime));
    }

    public async Task<IEnumerable<VehicleDispatchResponseDto>> GetDispatchesByVehicleAsync(string fieldVehicleId)
    {
        var results = await dispatches.FindAsync(d => d.FieldVehicleId == fieldVehicleId);
        return await MapDispatchesAsync(results.OrderByDescending(d => d.DepartureDatetime));
    }

    public async Task<int> GetPendingDispatchCountAsync()
    {
        var results = await dispatches.FindAsync(d => d.Status == VehicleDispatchStatus.Pending);
        return results.Count();
    }

    // Oldest-first — the request that's been waiting longest surfaces first for a fleet manager
    // triaging the approvals queue, unlike the other list methods here which show newest first.
    public async Task<IEnumerable<VehicleDispatchResponseDto>> GetPendingDispatchesAsync()
    {
        var results = await dispatches.FindAsync(d => d.Status == VehicleDispatchStatus.Pending);
        return await MapDispatchesAsync(results.OrderBy(d => d.DepartureDatetime));
    }

    public async Task<VehicleDispatchResponseDto> RequestDispatchAsync(CreateVehicleDispatchDto dto, string? requestedByUserId = null)
    {
        var vehicle = await vehicles.GetByIdAsync(dto.FieldVehicleId) ?? throw new KeyNotFoundException($"Field vehicle {dto.FieldVehicleId} not found.");

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
            DepartureOdometer = vehicle.CurrentOdometer,
            FuelLevelOut = dto.FuelLevelOut,
            Notes = dto.Notes,
            Status = VehicleDispatchStatus.Pending,
            // From the authenticated caller (ClaimTypes.NameIdentifier), never trusted from the
            // request body — used to notify the requester when a fleet manager approves this.
            RequestedByUserId = requestedByUserId,
        };
        var created = await dispatches.CreateAsync(dispatch);

        // Best-effort — CreateAlertAsync already swallows/logs its own failures, so a ticketing
        // outage never blocks the dispatch request itself. requiredPermission: "fleet.write"
        // scopes the alert to Fleet Manager/Fleet Staff/Admin only (see AlertsController.CanSee)
        // — the same people the approve/reject endpoints below already restrict action to.
        var schema = CurrentTenantSchema();
        if (schema != null)
        {
            var assignment = await operationsServiceClient.GetAssignmentAsync(schema, dto.AssignmentId);
            var assignmentLabel = assignment != null
                ? assignment.Title + (string.IsNullOrWhiteSpace(assignment.LocationName) ? "" : $" ({assignment.LocationName})")
                : $"assignment {dto.AssignmentId}"; // fallback if operations-service is unreachable

            var message = $"{dto.DriverName} requested {vehicle.Make} {vehicle.Model} ({vehicle.RegistrationNumber}) for {assignmentLabel}.";

            await ticketingClient.CreateAlertAsync(
                tenantSchema: schema,
                source: "FleetDispatchRequest",
                severity: "Info",
                title: $"Vehicle request — {vehicle.RegistrationNumber}",
                message: message,
                requiredPermission: "fleet.write");

            // The alert above is a shared, permission-scoped list (Requests & Approvals) — it has
            // no single "owner" to route through the Notification pipeline, so it never gets an
            // SMS/email on its own. Fan a real per-user notification out to everyone who can act
            // on it (fleet.write), which does ride the SMS/email pipeline automatically.
            var fleetManagers = await userServiceClient.GetUsersByRoleNameAsync(schema, "Fleet Manager");
            foreach (var manager in fleetManagers)
            {
                await ticketingClient.NotifyUserAsync(schema, manager.Id, "fleet_dispatch_request",
                    $"New vehicle request awaiting your approval — {vehicle.RegistrationNumber}. {message}");
            }
        }

        return await MapDispatchAsync(created);
    }

    public async Task<VehicleDispatchResponseDto> ApproveDispatchAsync(string dispatchId, string approvedBy, string? approvedByUserId = null)
    {
        var dispatch = await dispatches.GetByIdAsync(dispatchId) ?? throw new KeyNotFoundException($"Dispatch {dispatchId} not found.");
        if (dispatch.Status != VehicleDispatchStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be approved.");

        if (await HasOpenDispatchAsync(dispatch.FieldVehicleId, excludeDispatchId: dispatch.Id))
            throw new InvalidOperationException("This vehicle now has another pending or active dispatch.");

        dispatch.Status = VehicleDispatchStatus.Dispatched;
        dispatch.ApprovedBy = approvedBy;
        dispatch.ApprovedAt = DateTime.UtcNow;
        var updated = await dispatches.UpdateAsync(dispatch);

        var vehicle = await vehicles.GetByIdAsync(dispatch.FieldVehicleId);
        if (vehicle != null)
        {
            vehicle.CurrentOdometer = dispatch.DepartureOdometer;
            vehicle.Status = FieldVehicleStatus.Dispatched;
            await vehicles.UpdateAsync(vehicle);
        }

        // Best-effort — notify the original requester their dispatch was approved. NotifyUserAsync
        // and SendEmailAsync already swallow/log their own failures, so a ticketing/user-service
        // outage never blocks the approval itself (same pattern as CreateAlertAsync above).
        await NotifyRequesterOfApprovalAsync(dispatch, vehicle);

        // Also confirm to the approving manager themselves that their approval went through —
        // scoped to just this one person (approvedByUserId), never broadcast to other managers.
        if (!string.IsNullOrWhiteSpace(approvedByUserId))
        {
            var schema = CurrentTenantSchema();
            if (schema != null)
            {
                await ticketingClient.NotifyUserAsync(schema, approvedByUserId, "fleet_dispatch_approved_confirm",
                    $"You approved the request for vehicle {vehicle?.RegistrationNumber ?? dispatch.FieldVehicleId}.");
            }
        }

        return await MapDispatchAsync(updated!);
    }

    // Skips gracefully (with a log) when the dispatch has no RequestedByUserId — older dispatches
    // created before this field existed, or ones requested with no authenticated user available.
    private async Task NotifyRequesterOfApprovalAsync(VehicleDispatch dispatch, FieldVehicle? vehicle)
    {
        if (string.IsNullOrWhiteSpace(dispatch.RequestedByUserId))
        {
            logger.LogInformation("Dispatch {DispatchId} has no RequestedByUserId — skipping approval notification.", dispatch.Id);
            return;
        }

        var schema = CurrentTenantSchema();
        if (schema == null)
        {
            logger.LogWarning("No tenant schema available on approve — skipping approval notification for dispatch {DispatchId}.", dispatch.Id);
            return;
        }

        var contact = await userServiceClient.GetUserContactAsync(schema, dispatch.RequestedByUserId);
        if (contact == null)
        {
            logger.LogWarning("Could not resolve contact info for user {UserId} — skipping approval notification for dispatch {DispatchId}.", dispatch.RequestedByUserId, dispatch.Id);
            return;
        }

        var registration = vehicle?.RegistrationNumber ?? "your vehicle";
        // Departure is a date-only pick in the request form (no time input), so DepartureDatetime
        // is always midnight — show just the date, a "00:00" here would be meaningless noise.
        var message = $"Your request to use vehicle {registration} has been approved. Departure: {dispatch.DepartureDatetime:dd MMM yyyy}.";

        // In-app notification — ticketing-service's NotificationService.SendAsync also fires the
        // SMS leg centrally for every notification, so no separate SMS call is needed here.
        await ticketingClient.NotifyUserAsync(schema, dispatch.RequestedByUserId, "FleetDispatchApproved", message);

        if (!string.IsNullOrWhiteSpace(contact.Email))
        {
            await ticketingClient.SendEmailAsync(
                to: [contact.Email],
                subject: $"Vehicle request approved — {registration}",
                bodyHtml: $"<p>Hi {System.Net.WebUtility.HtmlEncode(contact.Name)},</p><p>{System.Net.WebUtility.HtmlEncode(message)}</p>");
        }
    }

    public async Task<VehicleDispatchResponseDto> RejectDispatchAsync(string dispatchId)
    {
        var dispatch = await dispatches.GetByIdAsync(dispatchId) ?? throw new KeyNotFoundException($"Dispatch {dispatchId} not found.");
        if (dispatch.Status != VehicleDispatchStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be rejected.");

        dispatch.Status = VehicleDispatchStatus.Rejected;
        var updated = await dispatches.UpdateAsync(dispatch);
        return await MapDispatchAsync(updated!);
    }

    public async Task<VehicleDispatchResponseDto> LogReturnAsync(string dispatchId, LogReturnDto dto)
    {
        var dispatch = await dispatches.GetByIdAsync(dispatchId) ?? throw new KeyNotFoundException($"Dispatch {dispatchId} not found.");
        if (dispatch.Status != VehicleDispatchStatus.Dispatched)
            throw new InvalidOperationException("Can only log return on an active dispatch.");

        dispatch.ReturnDatetime = dto.ReturnDatetime;
        dispatch.ReturnOdometer = dto.ReturnOdometer;
        dispatch.FuelLevelIn = dto.FuelLevelIn;
        if (dto.Notes is not null) dispatch.Notes = dto.Notes;
        dispatch.Status = VehicleDispatchStatus.Returned;
        var updated = await dispatches.UpdateAsync(dispatch);

        var vehicle = await vehicles.GetByIdAsync(dispatch.FieldVehicleId);
        if (vehicle != null)
        {
            vehicle.CurrentOdometer = dto.ReturnOdometer;
            vehicle.Status = FieldVehicleStatus.Available;
            await vehicles.UpdateAsync(vehicle);
        }

        return await MapDispatchAsync(updated!);
    }

    public async Task<VehicleDispatchResponseDto> CancelDispatchAsync(string dispatchId)
    {
        var dispatch = await dispatches.GetByIdAsync(dispatchId) ?? throw new KeyNotFoundException($"Dispatch {dispatchId} not found.");
        if (dispatch.Status is not (VehicleDispatchStatus.Pending or VehicleDispatchStatus.Dispatched))
            throw new InvalidOperationException("Only pending or active dispatches can be cancelled.");

        var wasDispatched = dispatch.Status == VehicleDispatchStatus.Dispatched;
        dispatch.Status = VehicleDispatchStatus.Cancelled;
        var updated = await dispatches.UpdateAsync(dispatch);

        // A Pending request never marked the vehicle unavailable, so only free it here
        // if this cancellation is actually ending an active (Dispatched) use.
        if (wasDispatched)
        {
            var vehicle = await vehicles.GetByIdAsync(dispatch.FieldVehicleId);
            if (vehicle != null)
            {
                vehicle.Status = FieldVehicleStatus.Available;
                await vehicles.UpdateAsync(vehicle);
            }
        }

        return await MapDispatchAsync(updated!);
    }

    private async Task<bool> HasOpenDispatchAsync(string fieldVehicleId, string? excludeDispatchId)
    {
        var open = await dispatches.FindAsync(d =>
            d.FieldVehicleId == fieldVehicleId &&
            (d.Status == VehicleDispatchStatus.Pending || d.Status == VehicleDispatchStatus.Dispatched));

        return open.Any(d => d.Id != excludeDispatchId);
    }

    // ─── Fuel logs ───────────────────────────────────────────────────────────

    public async Task<IEnumerable<DispatchFuelLogResponseDto>> GetDispatchFuelLogsAsync(string dispatchId)
    {
        var logs = await fuelLogs.FindAsync(f => f.DispatchId == dispatchId);
        return logs.OrderByDescending(f => f.LoggedAt).Select(MapFuelLog);
    }

    public async Task<DispatchFuelLogResponseDto> AddDispatchFuelLogAsync(string dispatchId, CreateDispatchFuelLogDto dto)
    {
        _ = await dispatches.GetByIdAsync(dispatchId) ?? throw new KeyNotFoundException($"Dispatch {dispatchId} not found.");

        var log = new DispatchFuelLog
        {
            DispatchId = dispatchId,
            AmountLitres = dto.AmountLitres,
            CostKes = dto.CostKes,
            Location = dto.Location,
            LoggedAt = dto.LoggedAt ?? DateTime.UtcNow,
            Notes = dto.Notes,
        };
        var created = await fuelLogs.CreateAsync(log);
        return MapFuelLog(created);
    }

    // ─── Mappers ─────────────────────────────────────────────────────────────

    private static FieldVehicleResponseDto MapVehicle(FieldVehicle v) => new()
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
        ServiceIntervalKm = v.ServiceIntervalKm,
        NextServiceOdometer = v.NextServiceOdometer,
        IsServiceDueByMileage = v.IsServiceDueByMileage,
        InsuranceExpiry = v.InsuranceExpiry,
        InsuranceExpiryDate = v.InsuranceExpiryDate,
        InspectionExpiryDate = v.InspectionExpiryDate,
        Notes = v.Notes,
        CreatedAt = v.CreatedAt,
        UpdatedAt = v.UpdatedAt,
    };

    private async Task<VehicleDispatchResponseDto> MapDispatchAsync(VehicleDispatch d)
    {
        var vehicle = await vehicles.GetByIdAsync(d.FieldVehicleId);
        var logs = await fuelLogs.FindAsync(f => f.DispatchId == d.Id);
        return BuildDispatchDto(d, vehicle, logs);
    }

    private async Task<IEnumerable<VehicleDispatchResponseDto>> MapDispatchesAsync(IEnumerable<VehicleDispatch> list)
    {
        var result = new List<VehicleDispatchResponseDto>();
        foreach (var d in list)
        {
            var vehicle = await vehicles.GetByIdAsync(d.FieldVehicleId);
            var logs = await fuelLogs.FindAsync(f => f.DispatchId == d.Id);
            result.Add(BuildDispatchDto(d, vehicle, logs));
        }
        return result;
    }

    private static VehicleDispatchResponseDto BuildDispatchDto(VehicleDispatch d, FieldVehicle? vehicle, IEnumerable<DispatchFuelLog> logs) => new()
    {
        Id = d.Id,
        AssignmentId = d.AssignmentId,
        FieldVehicleId = d.FieldVehicleId,
        VehicleRegistration = vehicle?.RegistrationNumber ?? string.Empty,
        VehicleMake = vehicle?.Make ?? string.Empty,
        VehicleModel = vehicle?.Model ?? string.Empty,
        VehicleType = vehicle?.Type.ToString() ?? string.Empty,
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
        FuelLogs = logs.OrderByDescending(f => f.LoggedAt).Select(MapFuelLog).ToList(),
    };

    private static DispatchFuelLogResponseDto MapFuelLog(DispatchFuelLog f) => new()
    {
        Id = f.Id,
        DispatchId = f.DispatchId,
        AmountLitres = f.AmountLitres,
        CostKes = f.CostKes,
        Location = f.Location,
        LoggedAt = f.LoggedAt,
        Notes = f.Notes,
    };
}
