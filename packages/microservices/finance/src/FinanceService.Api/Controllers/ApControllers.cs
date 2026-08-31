using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Api.Controllers;

// ── Suppliers ──
public class SuppliersController : BaseFinanceController
{
    private readonly FinanceDbContext _db;
    public SuppliersController(FinanceDbContext db) => _db = db;

    [HttpGet("suppliers")]
    public async Task<IActionResult> List() =>
        Ok(ApiResponse<object>.Ok(await _db.Suppliers.OrderBy(s => s.Name).ToListAsync()));

    [HttpPost("suppliers")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] Supplier dto)
    {
        dto.Id = Guid.NewGuid().ToString(); dto.CreatedBy = CurrentUserId;
        _db.Suppliers.Add(dto);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Supplier>.Ok(dto, "Supplier created."));
    }

    [HttpGet("payment-authority")]
    public async Task<IActionResult> Tiers() =>
        Ok(ApiResponse<object>.Ok(await _db.PaymentApprovalTiers.OrderBy(t => t.StepNumber).ToListAsync()));

    [HttpPatch("suppliers/{id}/deactivate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Deactivate(string id)
    {
        var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound(ApiResponse<object>.Fail("Supplier not found.", 404));
        s.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Supplier>.Ok(s, $"{s.Name} deactivated."));
    }

    [HttpPatch("suppliers/{id}/activate")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Activate(string id)
    {
        var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound(ApiResponse<object>.Fail("Supplier not found.", 404));
        s.IsActive = true;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Supplier>.Ok(s, $"{s.Name} activated."));
    }
}

// ── Supplier invoices (payables) ──
public class SupplierInvoicesController : BaseFinanceController
{
    private readonly ISupplierInvoiceService _svc;
    public SupplierInvoicesController(ISupplierInvoiceService svc) => _svc = svc;

    [HttpGet("supplier-invoices")]
    public async Task<IActionResult> List() => Ok(ApiResponse<List<SupplierInvoiceReadDto>>.Ok(await _svc.ListAsync()));

    [HttpGet("supplier-invoices/{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var b = await _svc.GetAsync(id);
        return b == null ? NotFound(ApiResponse<object>.Fail("Not found.", 404)) : Ok(ApiResponse<SupplierInvoiceReadDto>.Ok(b));
    }

    [HttpPost("supplier-invoices")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] CreateSupplierInvoiceDto dto)
        => Ok(ApiResponse<SupplierInvoiceReadDto>.Ok(await _svc.CreateAsync(dto, CurrentUserId), "Supplier invoice recorded."));

    /// <summary>Cancels the bill, reversing its GL posting if it had one. finance.approve, matching
    /// approve: cancelling reverses a posted journal without a second approval of its own.</summary>
    [HttpPost("supplier-invoices/{id}/cancel")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Cancel(string id, [FromBody] CancelSupplierInvoiceDto dto)
        => Ok(ApiResponse<SupplierInvoiceReadDto>.Ok(
            await _svc.CancelAsync(id, dto.Reason, CurrentUserId), "Supplier invoice cancelled."));

    [HttpPost("supplier-invoices/{id}/approve")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Approve(string id)
        => Ok(ApiResponse<SupplierInvoiceReadDto>.Ok(await _svc.ApproveAsync(id, CurrentUserId), "Approved & posted to payables."));

    /// <summary>Records the 3-way match result Procurement owns (Finance only stores it, and blocks approval
    /// while the match is in Exception). Called by the Procurement write-back seam after a match run.</summary>
    [HttpPost("supplier-invoices/{id}/match-status")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> SetMatchStatus(string id, [FromBody] SetMatchStatusDto dto)
    {
        if (!Enum.TryParse<ThreeWayMatchStatus>(dto.Status, true, out var status))
            return BadRequest(ApiResponse<object>.Fail($"Unknown match status '{dto.Status}'.", 400));
        return Ok(ApiResponse<SupplierInvoiceReadDto>.Ok(
            await _svc.SetMatchStatusAsync(id, status, dto.Note, CurrentUserId), "3-way match result recorded."));
    }
}

// ── Payment vouchers ──
public class VouchersController : BaseFinanceController
{
    private readonly IPaymentVoucherService _svc;
    public VouchersController(IPaymentVoucherService svc) => _svc = svc;

    [HttpGet("vouchers")]
    public async Task<IActionResult> List() => Ok(ApiResponse<List<VoucherReadDto>>.Ok(await _svc.ListAsync()));

    [HttpPost("vouchers")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] CreateVoucherDto dto)
        => Ok(ApiResponse<VoucherReadDto>.Ok(await _svc.CreateAsync(dto, CurrentUserId), "Voucher raised — routed by amount."));

    [HttpPost("vouchers/{id}/approve")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Approve(string id)
        => Ok(ApiResponse<VoucherReadDto>.Ok(await _svc.ApproveAsync(id, CurrentUserId, ApprovalCtx), "Voucher approved."));

    [HttpPost("vouchers/{id}/pay")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Pay(string id)
        => Ok(ApiResponse<VoucherReadDto>.Ok(await _svc.PayAsync(id, CurrentUserId), "Voucher paid & posted."));
}
