using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.BackgroundServices;

// The vehicle-side counterpart to LicenseExpiryBackgroundService (#375): driver licences got a
// proper banded-alert sweep, but nothing equivalent ever read Truck.InsuranceExpiryDate,
// Truck.InspectionExpiryDate, or FieldVehicle.InspectionExpiryDate for expiry — a truck or field
// vehicle could be dispatched on lapsed cover with no alert, no dashboard flag, no log line, ever.
// FieldVehicle.InsuranceExpiry was a free-text string until this same PR added
// InsuranceExpiryDate alongside it (backfilled by migration) — that is almost certainly why no
// sweep existed for it at all: a string can't be compared in SQL. Reuses
// LicenseExpiryBackgroundService.ExpiryBand for the same alert-title-dedup-stability reason (#219)
// and ITicketingServiceClient.CreateAlertAsync for the same reason FieldVehicleService.
// RequestDispatchAsync already does — there is no local alerts table in this service.
public class VehicleExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<VehicleExpiryBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var schema in await GetTenantSchemasAsync())
            {
                try
                {
                    await CheckExpiringVehiclesForSchemaAsync(schema);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Vehicle expiry check failed for schema {Schema} — will retry in 24 hours", schema);
                }
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task<List<string>> GetTenantSchemasAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetServiceDbContext>();
        return await db.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync();
    }

    // Same reasoning as LicenseExpiryBackgroundService: a background job has no HTTP request to
    // resolve a tenant schema from, so it has to be pinned explicitly per schema here.
    private async Task CheckExpiringVehiclesForSchemaAsync(string schema)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetServiceDbContext>();
        var ticketingClient = scope.ServiceProvider.GetRequiredService<ITicketingServiceClient>();

        await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (GetTenantSchemasAsync) above, not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
        await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002

        var warningDate = DateTime.UtcNow.AddDays(30);

        // A fleet's own truck/field-vehicle count is small (dozens, not millions) — filtering to
        // just !IsDeleted at the DB level and doing the rest of the eligibility/classification
        // logic in C# (below) trades a negligible amount of over-fetching for the same
        // testability ExpiryBand already gets: FindingsForTruck/FindingsForFieldVehicle are pure
        // functions with no DB dependency, unlike an inline EF LINQ .Where() clause.
        var trucks = await db.Trucks.Where(t => !t.IsDeleted).ToListAsync();
        var fieldVehicles = await db.FieldVehicles.Where(v => !v.IsDeleted).ToListAsync();

        var alertCount = 0;
        foreach (var truck in trucks.Where(IsEligible))
        {
            foreach (var finding in FindingsForTruck(truck, warningDate))
            {
                await RaiseFindingAsync(ticketingClient, schema, truck.LicensePlate, finding);
                alertCount++;
            }
        }

        foreach (var vehicle in fieldVehicles.Where(IsEligible))
        {
            foreach (var finding in FindingsForFieldVehicle(vehicle, warningDate))
            {
                await RaiseFindingAsync(ticketingClient, schema, vehicle.RegistrationNumber, finding);
                alertCount++;
            }
        }

        if (alertCount > 0)
            logger.LogInformation("{Count} vehicle insurance/inspection findings in {Schema}", alertCount, schema);

        await db.Database.CloseConnectionAsync();
    }

    // Decommissioned/out-of-service assets aren't dispatched, so a lapsed date on one of them
    // carries none of the legal exposure a vehicle actually on the road would — alerting on it
    // would just be noise nobody can act on.
    internal static bool IsEligible(Truck truck) =>
        truck.Status != TruckStatus.Decommissioned && truck.Status != TruckStatus.OutOfService;

    internal static bool IsEligible(FieldVehicle vehicle) =>
        vehicle.Status != FieldVehicleStatus.Decommissioned;

    internal enum FindingKind { Insurance, Inspection, InsuranceUnreadable }

    internal readonly record struct VehicleFinding(FindingKind Kind, DateTime? ExpiryDate);

    internal static IEnumerable<VehicleFinding> FindingsForTruck(Truck truck, DateTime warningDate)
    {
        if (truck.InsuranceExpiryDate.HasValue && truck.InsuranceExpiryDate <= warningDate)
            yield return new VehicleFinding(FindingKind.Insurance, truck.InsuranceExpiryDate);
        if (truck.InspectionExpiryDate.HasValue && truck.InspectionExpiryDate <= warningDate)
            yield return new VehicleFinding(FindingKind.Inspection, truck.InspectionExpiryDate);
    }

    internal static IEnumerable<VehicleFinding> FindingsForFieldVehicle(FieldVehicle vehicle, DateTime warningDate)
    {
        if (vehicle.InsuranceExpiryDate.HasValue && vehicle.InsuranceExpiryDate <= warningDate)
        {
            yield return new VehicleFinding(FindingKind.Insurance, vehicle.InsuranceExpiryDate);
        }
        else if (vehicle.InsuranceExpiryDate is null && !string.IsNullOrWhiteSpace(vehicle.InsuranceExpiry))
        {
            // A legacy free-text value the backfill migration couldn't parse into a real date —
            // the record needs a human to fix it, not a false "compliant" or a false "expired".
            // Mirrors expiry.js's 'unknown' state from #376 rather than inventing a fourth
            // convention.
            yield return new VehicleFinding(FindingKind.InsuranceUnreadable, null);
        }

        if (vehicle.InspectionExpiryDate.HasValue && vehicle.InspectionExpiryDate <= warningDate)
            yield return new VehicleFinding(FindingKind.Inspection, vehicle.InspectionExpiryDate);
    }

    private async Task RaiseFindingAsync(ITicketingServiceClient ticketingClient, string schema, string identifier, VehicleFinding finding)
    {
        if (finding.Kind == FindingKind.InsuranceUnreadable)
        {
            logger.LogWarning("Vehicle {Identifier} has an unreadable insurance expiry value ({Schema})", identifier, schema);
            await ticketingClient.CreateAlertAsync(
                tenantSchema: schema,
                source: "VehicleInsurance",
                severity: "Warning",
                title: $"Insurance expiry unreadable — {identifier}",
                message: $"{identifier}'s recorded insurance expiry could not be read as a date. Update the record with a valid expiry date.");
            return;
        }

        var kind = finding.Kind == FindingKind.Insurance ? "insurance" : "inspection";
        var source = finding.Kind == FindingKind.Insurance ? "VehicleInsurance" : "VehicleInspection";
        var expiryDate = finding.ExpiryDate!.Value;
        var daysLeft = (int)(expiryDate - DateTime.UtcNow).TotalDays;
        var expired = daysLeft <= 0;
        var band = LicenseExpiryBackgroundService.ExpiryBand(daysLeft);

        if (expired)
            logger.LogWarning("Vehicle {Identifier} {Kind} EXPIRED on {Date} ({Schema})", identifier, kind, expiryDate, schema);
        else
            logger.LogWarning("Vehicle {Identifier} {Kind} expires in {Days} days ({Date}) ({Schema})", identifier, kind, daysLeft, expiryDate, schema);

        await ticketingClient.CreateAlertAsync(
            tenantSchema: schema,
            source: source,
            severity: expired ? "Critical" : "Warning",
            title: expired
                ? $"Vehicle {kind} expired — {identifier}"
                : $"Vehicle {kind} expiring within {band} — {identifier}",
            message: expired
                ? $"{identifier}'s {kind} expired on {expiryDate:d}."
                : $"{identifier}'s {kind} expires on {expiryDate:d}.");
    }
}
