using FinanceService.Core.DTOs;
using FinanceService.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Api.Controllers;

// Read side for #216's field-level audit trail (FinanceAuditInterceptor). Gated by the same
// platform-wide "system.admin" policy as the gateway's access log (AuditLogController in
// user-service), since this exposes every field-level change to every ledger row — a strictly
// wider view than finance.read.
[Authorize(Policy = "system.admin")]
[ApiController]
[Asp.Versioning.ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/finance/audit-log")]
public class FinanceAuditLogController(FinanceDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? entity, [FromQuery] string? entityId, [FromQuery] string? actor,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.FinanceAuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(a => a.Entity == entity);
        if (!string.IsNullOrWhiteSpace(entityId)) query = query.Where(a => a.EntityId == entityId);
        if (!string.IsNullOrWhiteSpace(actor)) query = query.Where(a => a.Actor == actor);
        if (from.HasValue) query = query.Where(a => a.At >= from.Value);
        if (to.HasValue) query = query.Where(a => a.At <= to.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.At)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id, a.Entity, a.EntityId, a.Action, a.Actor, a.Details, a.At,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items, totalCount, page, pageSize }));
    }
}
