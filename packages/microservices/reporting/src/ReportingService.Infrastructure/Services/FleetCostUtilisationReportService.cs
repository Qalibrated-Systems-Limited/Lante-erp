using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

// Report #6: fleet-cost-utilisation. Pulls trucks (for plate/model lookup) and trips (either a
// single truck's trips, or all trips paged through), filters by Trip.Date if from/to supplied
// (Trip has no separate StartedAt/CompletedAt — Date is the only business-date field), then
// aggregates TotalCost/Revenue/Profit/TotalMileage grouped by truck plus a grand-total row.
public class FleetCostUtilisationReportService(
    IFleetServiceClient fleet,
    ILogger<FleetCostUtilisationReportService> logger) : IFleetCostUtilisationReportService
{
    private const int PageSize = 100;

    public async Task<FleetCostUtilisationReportDto> GetAsync(DateTime? from, DateTime? to, string? truckId)
    {
        var report = new FleetCostUtilisationReportDto { From = from, To = to };

        List<TruckDto> trucks;
        try
        {
            trucks = await fleet.GetTrucksAsync() ?? new List<TruckDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch trucks");
            report.Warnings.Add("Failed to fetch truck details from FleetService; plate/model columns may be blank.");
            trucks = new List<TruckDto>();
        }
        var truckLookup = trucks.ToDictionary(t => t.Id, t => t);

        var trips = new List<TripResponseDto>();
        try
        {
            if (!string.IsNullOrWhiteSpace(truckId))
            {
                trips = await fleet.GetTripsByTruckAsync(truckId) ?? new List<TripResponseDto>();
            }
            else
            {
                var page = 1;
                while (true)
                {
                    var pageResult = await fleet.GetTripsPageAsync(page, PageSize);
                    if (pageResult == null || pageResult.Items.Count == 0) break;
                    trips.AddRange(pageResult.Items);
                    if (page >= pageResult.TotalPages || pageResult.Items.Count < PageSize) break;
                    page++;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch trips");
            report.Warnings.Add("Failed to fetch trips from FleetService; results may be incomplete.");
        }

        if (from.HasValue) trips = trips.Where(t => t.Date >= from.Value).ToList();
        if (to.HasValue) trips = trips.Where(t => t.Date <= to.Value).ToList();

        var grouped = trips
            .GroupBy(t => t.TruckId)
            .Select(g =>
            {
                truckLookup.TryGetValue(g.Key, out var truck);
                return new FleetTruckCostRowDto
                {
                    TruckId = g.Key,
                    LicensePlate = truck?.LicensePlate,
                    Model = truck?.Model,
                    TripCount = g.Count(),
                    TotalCost = g.Sum(t => t.TotalCost),
                    TotalRevenue = g.Sum(t => t.Revenue),
                    TotalProfit = g.Sum(t => t.Profit),
                    TotalMileage = g.Sum(t => t.TotalMileage ?? 0),
                };
            })
            .OrderByDescending(r => r.TotalProfit)
            .ToList();

        report.Trucks = grouped;
        report.Totals = new FleetTruckCostRowDto
        {
            TruckId = "TOTAL",
            TripCount = grouped.Sum(r => r.TripCount),
            TotalCost = grouped.Sum(r => r.TotalCost),
            TotalRevenue = grouped.Sum(r => r.TotalRevenue),
            TotalProfit = grouped.Sum(r => r.TotalProfit),
            TotalMileage = grouped.Sum(r => r.TotalMileage),
        };

        return report;
    }
}
