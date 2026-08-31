using ComplianceService.Core.DTOs.SopLibrary;
using ComplianceService.Core.Entities;
using ComplianceService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ComplianceService.Infrastructure.Services;

public class SopLibraryService(ComplianceDbContext context) : ISopLibraryService
{
    // "QSL" is hardcoded because this module is single-tenant in practice today (only QSL uses
    // it). A real multi-tenant rollout would need to resolve this from the caller's actual
    // tenant/company short code instead of a constant — revisit before onboarding a second
    // tenant onto the SOP Library.
    private const string TenantPrefix = "QSL";

    public async Task<SopDocument> CreateAsync(CreateSopDto dto, string createdBy)
    {
        // The DbContext has EnableRetryOnFailure configured, so a plain
        // Database.BeginTransactionAsync() throws — EF requires manual transactions to run
        // through the execution strategy so the whole block can be retried as one unit.
        var strategy = context.Database.CreateExecutionStrategy();
        SopDocument entity = null!;

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            // pg_advisory_xact_lock serializes concurrent Code generation so two requests can't
            // both read the same COUNT(*) and mint the same code before either commits. The lock
            // is released automatically at transaction end (commit or rollback). Key is a
            // constant, not scoped to this tenant's schema, so it also safely serializes across
            // tenants sharing this Postgres instance — an acceptable trade-off since SOP creation
            // is low-frequency.
            await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('sop_library_code_seq'))");

            var count = await context.SopDocuments.CountAsync();
            var code = $"{TenantPrefix}/QP/{count + 1}";

            entity = new SopDocument
            {
                Code = code,
                Title = dto.Title,
                Department = dto.Department,
                Category = dto.Category,
                Version = "Rev 1",
                FileUrl = dto.FileUrl,
                LastReviewed = DateTime.UtcNow,
                NextReview = dto.NextReview,
                CreatedBy = createdBy,
            };

            context.SopDocuments.Add(entity);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        });

        return entity;
    }
}
