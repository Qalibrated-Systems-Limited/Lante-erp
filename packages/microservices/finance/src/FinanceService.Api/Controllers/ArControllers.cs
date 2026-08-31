using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Api.Controllers;

// ── Customers ──
public class CustomersController : BaseFinanceController
{
    private readonly FinanceDbContext _db;
    public CustomersController(FinanceDbContext db) => _db = db;

    [HttpGet("customers")]
    public async Task<IActionResult> List() =>
        Ok(ApiResponse<object>.Ok(await _db.Customers.OrderBy(c => c.Name).ToListAsync()));

    [HttpPost("customers")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] Customer dto)
    {
        dto.Id = Guid.NewGuid().ToString(); dto.CreatedBy = CurrentUserId;
        _db.Customers.Add(dto);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Customer>.Ok(dto, "Customer created."));
    }

    [HttpGet("tax-categories")]
    public async Task<IActionResult> TaxCategories() =>
        Ok(ApiResponse<object>.Ok(await _db.TaxCategories.OrderBy(t => t.Code).ToListAsync()));

    [HttpPatch("customers/{id}/deactivate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Deactivate(string id)
    {
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound(ApiResponse<object>.Fail("Customer not found.", 404));
        c.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Customer>.Ok(c, $"{c.Name} deactivated."));
    }

    [HttpPatch("customers/{id}/activate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Activate(string id)
    {
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound(ApiResponse<object>.Fail("Customer not found.", 404));
        c.IsActive = true;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Customer>.Ok(c, $"{c.Name} activated."));
    }

    [HttpPatch("tax-categories/{id}/deactivate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> DeactivateTaxCategory(string id)
    {
        var t = await _db.TaxCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound(ApiResponse<object>.Fail("Tax category not found.", 404));
        t.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<TaxCategory>.Ok(t, $"{t.Code} deactivated."));
    }

    [HttpPatch("tax-categories/{id}/activate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> ActivateTaxCategory(string id)
    {
        var t = await _db.TaxCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound(ApiResponse<object>.Fail("Tax category not found.", 404));
        t.IsActive = true;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<TaxCategory>.Ok(t, $"{t.Code} activated."));
    }
}

// ── Invoices ──
public class InvoicesController : BaseFinanceController
{
    private readonly IInvoiceService _svc;
    public InvoicesController(IInvoiceService svc) => _svc = svc;

    [HttpGet("invoices")]
    public async Task<IActionResult> List() => Ok(ApiResponse<List<InvoiceReadDto>>.Ok(await _svc.ListAsync()));

    [HttpGet("invoices/{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var i = await _svc.GetAsync(id);
        return i == null ? NotFound(ApiResponse<object>.Fail("Invoice not found.", 404)) : Ok(ApiResponse<InvoiceReadDto>.Ok(i));
    }

    [HttpPost("invoices")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceDto dto)
        => Ok(ApiResponse<InvoiceReadDto>.Ok(await _svc.CreateAsync(dto, CurrentUserId), "Invoice drafted."));

    /// <summary>
    /// Cancels the invoice, reversing its GL posting if it had one.
    ///
    /// <para>finance.approve rather than finance.write, matching the twenty other approve-guarded
    /// endpoints in this service: cancelling an issued invoice reverses a posted journal, which is an
    /// approval-grade act, and it does so without a second approval step of its own. The cost is that
    /// whoever drafted an invoice cannot cancel their own draft — accepted, because splitting the
    /// endpoint by status would put the weaker policy on the same route as the stronger one.</para>
    /// </summary>
    [HttpPost("invoices/{id}/cancel")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Cancel(string id, [FromBody] CancelInvoiceDto dto)
        => Ok(ApiResponse<InvoiceReadDto>.Ok(
            await _svc.CancelAsync(id, dto.Reason, CurrentUserId), "Invoice cancelled."));

    /// Posts the invoice to the GL and submits it to eTIMS.
    [HttpPost("invoices/{id}/issue")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Issue(string id)
        => Ok(ApiResponse<InvoiceReadDto>.Ok(await _svc.IssueAsync(id, CurrentUserId), "Invoice issued, posted & submitted to eTIMS."));

    [HttpGet("debtors/aging")]
    public async Task<IActionResult> Aging([FromQuery] DateTime? asOf)
        => Ok(ApiResponse<List<DebtorAgingRowDto>>.Ok(await _svc.AgingAsync(asOf ?? DateTime.UtcNow)));
}

// ── Receipts ──
public class ReceiptsController : BaseFinanceController
{
    private readonly IReceiptService _svc;
    public ReceiptsController(IReceiptService svc) => _svc = svc;

    [HttpGet("receipts")]
    public async Task<IActionResult> List() => Ok(ApiResponse<List<ReceiptReadDto>>.Ok(await _svc.ListAsync()));

    [HttpPost("receipts")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] CreateReceiptDto dto)
        => Ok(ApiResponse<ReceiptReadDto>.Ok(await _svc.CreateAsync(dto, CurrentUserId), "Receipt recorded & allocated."));
}

// ── VAT ──
public class VatController : BaseFinanceController
{
    private readonly IVatService _svc;
    public VatController(IVatService svc) => _svc = svc;

    [HttpGet("vat/compute")]
    public async Task<IActionResult> Compute([FromQuery] string periodId)
        => Ok(ApiResponse<VatReturnDto>.Ok(await _svc.ComputeAsync(periodId)));

    [HttpPost("vat/file")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> File([FromQuery] string periodId)
        => Ok(ApiResponse<VatReturnDto>.Ok(await _svc.FileAsync(periodId, CurrentUserId), "VAT return filed."));
}
