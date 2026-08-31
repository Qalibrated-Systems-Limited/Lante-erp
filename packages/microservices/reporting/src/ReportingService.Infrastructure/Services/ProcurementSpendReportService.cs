using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

// Report #7: procurement-spend. Pages through GRN records (StoreService already supports
// FromDate/ToDate/SupplierId filters server-side) and the item master (for ItemId -> Category
// lookup, since GRN rows only carry ItemId), then aggregates LandedCost by supplier and by
// category. Each GRN row is already a single item/supplier/LandedCost line — no nested line
// items to unpack.
public class ProcurementSpendReportService(
    IStoreServiceClient stores,
    ILogger<ProcurementSpendReportService> logger) : IProcurementSpendReportService
{
    private const int PageSize = 100;

    public async Task<ProcurementSpendReportDto> GetAsync(DateTime? from, DateTime? to, string? supplierId, string? category)
    {
        var report = new ProcurementSpendReportDto { From = from, To = to };

        var grnRows = new List<GrnReadDto>();
        try
        {
            var page = 1;
            while (true)
            {
                var pageResult = await stores.GetGrnPageAsync(from, to, supplierId, page, PageSize);
                if (pageResult == null || pageResult.Items.Count == 0) break;
                grnRows.AddRange(pageResult.Items);
                if (page >= pageResult.TotalPages || pageResult.Items.Count < PageSize) break;
                page++;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch GRN records");
            report.Warnings.Add("Failed to fetch goods-received records from StoreService.");
        }

        var itemLookup = new Dictionary<string, ItemMasterReadDto>();
        try
        {
            var page = 1;
            while (true)
            {
                var pageResult = await stores.GetItemsPageAsync(page, PageSize);
                if (pageResult == null || pageResult.Items.Count == 0) break;
                foreach (var item in pageResult.Items) itemLookup[item.Id] = item;
                if (page >= pageResult.TotalPages || pageResult.Items.Count < PageSize) break;
                page++;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch item master");
            report.Warnings.Add("Failed to fetch item master from StoreService; category breakdown may be incomplete.");
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            grnRows = grnRows
                .Where(g => itemLookup.TryGetValue(g.ItemId, out var item)
                            && string.Equals(item.CategoryName, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        report.GrandTotal = grnRows.Sum(g => g.LandedCost);

        report.BySupplier = grnRows
            .GroupBy(g => g.SupplierId)
            .Select(g => new ProcurementSpendBySupplierDto
            {
                SupplierId = g.Key,
                SupplierName = g.First().SupplierName,
                TotalLandedCost = g.Sum(x => x.LandedCost),
                LineCount = g.Count(),
            })
            .OrderByDescending(r => r.TotalLandedCost)
            .ToList();

        // CategoryId -> CategoryName, derived once from whichever items we successfully looked up.
        var categoryNames = itemLookup.Values
            .GroupBy(i => i.CategoryId)
            .ToDictionary(g => g.Key, g => g.First().CategoryName);

        report.ByCategory = grnRows
            .GroupBy(g => itemLookup.TryGetValue(g.ItemId, out var item) ? item.CategoryId : null)
            .Select(g => new ProcurementSpendByCategoryDto
            {
                CategoryId = g.Key,
                CategoryName = g.Key != null && categoryNames.TryGetValue(g.Key, out var name) ? name : "Uncategorised",
                TotalLandedCost = g.Sum(x => x.LandedCost),
                LineCount = g.Count(),
            })
            .OrderByDescending(r => r.TotalLandedCost)
            .ToList();

        return report;
    }
}
