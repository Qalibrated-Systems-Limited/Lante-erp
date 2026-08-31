using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

using FinanceService.Core.Services;

namespace FinanceService.Infrastructure.Services;

public class DepreciationService : IDepreciationService
{
    private const string DepreciationExpenseAccountCode = "5600";
    private const string AccumulatedDepreciationAccountCode = "1520";

    private readonly FinanceDbContext _db;
    private readonly IJournalService _journals;
    public DepreciationService(FinanceDbContext db, IJournalService journals) { _db = db; _journals = journals; }

    public async Task<RunDepreciationResultDto> RunDepreciationAsync(string period, string? actorUserId)
    {
        // The "already run" check below is check-then-act: two concurrent calls for the same
        // period (the manual "Run Depreciation" button racing the monthly background tick, or two
        // pod replicas both ticking) can both pass it before either commits, each posting its own
        // GL journal — only the DepreciationEntries insert is protected by a unique index, so the
        // duplicate journal survives even though one of the two entry-sets fails. A pg_advisory_xact_lock
        // scoped to this period serializes concurrent runs so the check is effectively atomic; lock
        // releases automatically at transaction end. _journals.CreateAsync shares this DbContext, so
        // its SaveChangesAsync joins this same transaction instead of committing independently.
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtext({0}))", $"depreciation_run:{period}");

            var existing = await _db.DepreciationEntries.Where(e => e.Period == period).ToListAsync();
            if (existing.Count > 0)
            {
                return new RunDepreciationResultDto
                {
                    Period = period, AlreadyRun = true, AssetsProcessed = existing.Count,
                    TotalCharge = existing.Sum(e => e.Amount), JournalEntryId = existing.First().JournalEntryId,
                };
            }

            var entryDate = DepreciationRules.LastDayOfPeriod(period);
            var assets = await _db.FixedAssets.Include(a => a.Category)
                .Where(a => a.Status == "Active").ToListAsync();

            var charges = new List<(FixedAsset Asset, decimal Amount)>();
            foreach (var asset in assets)
            {
                var rate = asset.AnnualRateOverride ?? asset.Category?.AnnualRate ?? 0;
                // The arithmetic lives in DepreciationRules, which is pure and tested — this method
                // cannot be, because it holds a pg_advisory_xact_lock no test provider honours (#230).
                var amount = DepreciationRules.MonthlyCharge(rate, asset.AcquisitionCost, asset.AccumulatedDepreciation);
                if (amount <= 0) continue;
                charges.Add((asset, amount));
            }

            if (charges.Count == 0)
            {
                await transaction.CommitAsync();
                return new RunDepreciationResultDto { Period = period, AssetsProcessed = 0, TotalCharge = 0 };
            }

            var total = charges.Sum(c => c.Amount);
            var journal = await _journals.CreateAsync(new CreateJournalDto
            {
                EntryDate = entryDate,
                Description = $"Depreciation charge for {period}",
                SourceModule = "FixedAssets",
                SourceDocumentId = period,
                PostImmediately = true,
                Lines = new List<CreateJournalLineDto>
                {
                    new() { AccountCode = DepreciationExpenseAccountCode, Debit = total, Description = $"Depreciation — {period}" },
                    new() { AccountCode = AccumulatedDepreciationAccountCode, Credit = total, Description = $"Accumulated depreciation — {period}" },
                },
            }, actorUserId);

            foreach (var (asset, amount) in charges)
            {
                asset.AccumulatedDepreciation += amount;
                asset.UpdatedAt = DateTime.UtcNow;
                asset.UpdatedBy = actorUserId;
                _db.DepreciationEntries.Add(new DepreciationEntry
                {
                    AssetId = asset.Id, Period = period, Amount = amount,
                    AccumulatedAfter = asset.AccumulatedDepreciation,
                    NetBookValueAfter = asset.AcquisitionCost - asset.AccumulatedDepreciation,
                    JournalEntryId = journal.Id, CreatedBy = actorUserId, UpdatedBy = actorUserId,
                });
            }
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new RunDepreciationResultDto
            {
                Period = period, AssetsProcessed = charges.Count, TotalCharge = total, JournalEntryId = journal.Id,
            };
        });
    }

    public async Task<List<DepreciationEntryReadDto>> GetScheduleAsync(string? period = null)
    {
        var q = _db.DepreciationEntries.Include(e => e.Asset).AsQueryable();
        if (!string.IsNullOrWhiteSpace(period)) q = q.Where(e => e.Period == period);
        var entries = await q.OrderByDescending(e => e.Period).ThenBy(e => e.Asset!.AssetTag).ToListAsync();
        return entries.Select(e => new DepreciationEntryReadDto
        {
            Id = e.Id, AssetId = e.AssetId, AssetTag = e.Asset?.AssetTag ?? "", AssetDescription = e.Asset?.Description ?? "",
            Period = e.Period, Amount = e.Amount, AccumulatedAfter = e.AccumulatedAfter,
            NetBookValueAfter = e.NetBookValueAfter, JournalEntryId = e.JournalEntryId,
        }).ToList();
    }


}
