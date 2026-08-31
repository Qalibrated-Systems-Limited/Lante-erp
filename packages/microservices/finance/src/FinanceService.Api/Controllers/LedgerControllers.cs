using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Api.Controllers;

// ── Chart of Accounts ──────────────────────────────────────────────────────────
public class ChartOfAccountsController : BaseFinanceController
{
    private readonly FinanceDbContext _db;
    public ChartOfAccountsController(FinanceDbContext db) => _db = db;

    [HttpGet("chart-of-accounts")]
    public async Task<IActionResult> List()
    {
        var rows = await _db.ChartOfAccounts.Include(a => a.AccountType)
            .OrderBy(a => a.Code)
            .Select(a => new
            {
                a.Id, a.Code, a.Name, a.ParentId, a.IsDirectPosting, a.IsBank, a.IsActive,
                AccountType = a.AccountType!.Name,
                Classification = a.AccountType.Classification.ToString(),
            }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(rows, $"{rows.Count} accounts"));
    }

    [HttpPost("chart-of-accounts")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] ChartOfAccount dto)
    {
        dto.Id = Guid.NewGuid().ToString();
        dto.CreatedBy = CurrentUserId;
        _db.ChartOfAccounts.Add(dto);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<ChartOfAccount>.Ok(dto, "Account created."));
    }

    [HttpPatch("chart-of-accounts/{id}/deactivate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Deactivate(string id)
    {
        var a = await _db.ChartOfAccounts.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound(ApiResponse<object>.Fail("Account not found.", 404));
        a.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<ChartOfAccount>.Ok(a, $"{a.Code} deactivated."));
    }

    [HttpPatch("chart-of-accounts/{id}/activate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Activate(string id)
    {
        var a = await _db.ChartOfAccounts.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound(ApiResponse<object>.Fail("Account not found.", 404));
        a.IsActive = true;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<ChartOfAccount>.Ok(a, $"{a.Code} activated."));
    }
}

// ── Currencies ─────────────────────────────────────────────────────────────────
public class CurrenciesController : BaseFinanceController
{
    private readonly FinanceDbContext _db;
    private readonly IExchangeRateProvider _fx;
    public CurrenciesController(FinanceDbContext db, IExchangeRateProvider fx) { _db = db; _fx = fx; }

    [HttpGet("currencies")]
    public async Task<IActionResult> List() =>
        Ok(ApiResponse<object>.Ok(await _db.Currencies.OrderByDescending(c => c.IsBaseCurrency).ThenBy(c => c.Code).ToListAsync()));

    [HttpPost("currencies/refresh-rates")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Refresh()
    {
        var baseCcy = await _db.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency);
        if (baseCcy == null) return BadRequest(ApiResponse<object>.Fail("No base currency."));
        var others = await _db.Currencies.Where(c => !c.IsBaseCurrency).ToListAsync();
        var rates = await _fx.FetchRatesAsync(baseCcy.Code, others.Select(o => o.Code));
        foreach (var c in others)
            if (rates.TryGetValue(c.Code, out var r)) { c.ExchangeRate = r; c.Source = Core.Enums.CurrencySource.Api; c.LastFetchedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(others, "Rates refreshed (stub provider)."));
    }

    /// Manually set a currency's rate (Treasury). Rejected for the base currency (always 1).
    [HttpPost("currencies/{id}/rate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> SetRate(string id, [FromBody] SetRateDto dto)
    {
        var c = await _db.Currencies.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound(ApiResponse<object>.Fail("Currency not found.", 404));
        if (c.IsBaseCurrency) return BadRequest(ApiResponse<object>.Fail("The base currency rate is fixed at 1."));
        if (dto.Rate <= 0) return BadRequest(ApiResponse<object>.Fail("Rate must be positive."));
        c.ExchangeRate = dto.Rate;
        c.Source = Core.Enums.CurrencySource.Manual;
        c.LastFetchedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(c, $"{c.Code} rate set to {dto.Rate}."));
    }

    [HttpPatch("currencies/{id}/deactivate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Deactivate(string id)
    {
        var c = await _db.Currencies.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound(ApiResponse<object>.Fail("Currency not found.", 404));
        if (c.IsBaseCurrency) return BadRequest(ApiResponse<object>.Fail("The base currency cannot be deactivated."));
        c.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Currency>.Ok(c, $"{c.Code} deactivated."));
    }

    [HttpPatch("currencies/{id}/activate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Activate(string id)
    {
        var c = await _db.Currencies.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound(ApiResponse<object>.Fail("Currency not found.", 404));
        c.IsActive = true;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Currency>.Ok(c, $"{c.Code} activated."));
    }
}

// ── Fiscal years & periods ───────────────────────────────────────────────────────
public class FiscalYearsController : BaseFinanceController
{
    private readonly FinanceDbContext _db;
    public FiscalYearsController(FinanceDbContext db) => _db = db;

    [HttpGet("fiscal-years")]
    public async Task<IActionResult> List()
    {
        var years = await _db.FiscalYears.OrderByDescending(f => f.StartDate).ToListAsync();
        var periods = await _db.AccountingPeriods.OrderBy(p => p.PeriodNo).ToListAsync();
        var result = years.Select(y => new
        {
            y.Id, y.Name, y.StartDate, y.EndDate, Status = y.Status.ToString(),
            Periods = periods.Where(p => p.FiscalYearId == y.Id)
                .Select(p => new { p.Id, p.PeriodNo, p.Name, p.StartDate, p.EndDate, Status = p.Status.ToString() }),
        });
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("periods/{id}/close")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> ClosePeriod(string id)
    {
        var p = await _db.AccountingPeriods.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound(ApiResponse<object>.Fail("Period not found.", 404));
        p.Status = Core.Enums.PeriodStatus.Closed; p.LockedBy = CurrentUserId; p.LockedAt = DateTime.UtcNow;
        _db.PeriodCloseLogs.Add(new PeriodCloseLog { PeriodId = p.Id, ClosedBy = CurrentUserId, ClosedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { p.Id, Status = p.Status.ToString() }, $"Period {p.Name} closed."));
    }
}

// ── Cost centres ─────────────────────────────────────────────────────────────────
public class CostCentersController : BaseFinanceController
{
    private readonly FinanceDbContext _db;
    public CostCentersController(FinanceDbContext db) => _db = db;

    [HttpGet("cost-centres")]
    public async Task<IActionResult> List() =>
        Ok(ApiResponse<object>.Ok(await _db.CostCenters.OrderBy(c => c.Code).ToListAsync()));

    [HttpPost("cost-centres")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] CostCenter dto)
    {
        dto.Id = Guid.NewGuid().ToString(); dto.CreatedBy = CurrentUserId;
        _db.CostCenters.Add(dto);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<CostCenter>.Ok(dto, "Cost centre created."));
    }

    [HttpPatch("cost-centres/{id}/deactivate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Deactivate(string id)
    {
        var c = await _db.CostCenters.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound(ApiResponse<object>.Fail("Cost centre not found.", 404));
        c.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<CostCenter>.Ok(c, $"{c.Code} deactivated."));
    }

    [HttpPatch("cost-centres/{id}/activate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Activate(string id)
    {
        var c = await _db.CostCenters.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound(ApiResponse<object>.Fail("Cost centre not found.", 404));
        c.IsActive = true;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<CostCenter>.Ok(c, $"{c.Code} activated."));
    }
}
