using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.ServiceClients;

public class FleetServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<FleetServiceClient> logger)
    : BaseServiceClient(httpClientFactory, config, httpContextAccessor, logger), IFleetServiceClient
{
    protected override string ClientName => "FleetService";
    protected override string ConfigKey => "FleetService";

    public Task<List<TruckDto>?> GetTrucksAsync() =>
        GetAsync<List<TruckDto>>("/api/v1/Trucks");

    public Task<List<TripResponseDto>?> GetTripsByTruckAsync(string truckId) =>
        GetAsync<List<TripResponseDto>>($"/api/v1/Trips/truck/{Uri.EscapeDataString(truckId)}");

    public Task<FleetPagedResult<TripResponseDto>?> GetTripsPageAsync(int pageNumber, int pageSize) =>
        GetAsync<FleetPagedResult<TripResponseDto>>($"/api/v1/Trips{BuildQuery(new Dictionary<string, string?> { ["pageNumber"] = pageNumber.ToString(), ["pageSize"] = pageSize.ToString() })}");
}
