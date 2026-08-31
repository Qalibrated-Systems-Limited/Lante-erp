using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.ServiceClients;

public class StoreServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<StoreServiceClient> logger)
    : BaseServiceClient(httpClientFactory, config, httpContextAccessor, logger), IStoreServiceClient
{
    protected override string ClientName => "StoreService";
    protected override string ConfigKey => "StoreService";

    public Task<PagedResult<GrnReadDto>?> GetGrnPageAsync(DateTime? fromDate, DateTime? toDate, string? supplierId, int page, int pageSize) =>
        GetAsync<PagedResult<GrnReadDto>>($"/api/v1/grn{BuildQuery(new Dictionary<string, string?>
        {
            ["FromDate"] = fromDate?.ToString("o"),
            ["ToDate"] = toDate?.ToString("o"),
            ["SupplierId"] = supplierId,
            ["Page"] = page.ToString(),
            ["PageSize"] = pageSize.ToString(),
        })}");

    public Task<PagedResult<SupplierReadDto>?> GetSuppliersPageAsync(int page, int pageSize) =>
        GetAsync<PagedResult<SupplierReadDto>>($"/api/v1/suppliers{BuildQuery(new Dictionary<string, string?> { ["Page"] = page.ToString(), ["PageSize"] = pageSize.ToString() })}");

    public Task<PagedResult<ItemMasterReadDto>?> GetItemsPageAsync(int page, int pageSize) =>
        GetAsync<PagedResult<ItemMasterReadDto>>($"/api/v1/items{BuildQuery(new Dictionary<string, string?> { ["Page"] = page.ToString(), ["PageSize"] = pageSize.ToString() })}");
}
