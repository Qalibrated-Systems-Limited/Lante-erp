using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubcontractsService.Core.DTOs.Common;
using SubcontractsService.Core.DTOs.Retentions;
using SubcontractsService.Core.Entities;
using SubcontractsService.Core.Enums;
using SubcontractsService.Core.Interfaces.Services;

using SubcontractsService.Core.Services;

namespace SubcontractsService.Api.Controllers;

// SUB-007: certified invoices, retention balances, WHT deductions, payment dates.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payment-retentions")]
public class PaymentRetentionsController(ISubcontractsCrudService<PaymentRetention> retentions) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "subcontracts.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<PaymentRetentionReadDto>>>> GetAll(
        [FromQuery] string? awardId, [FromQuery] PaginationParameters parameters)
    {
        PaginatedResult<PaymentRetentionReadDto> result;
        if (string.IsNullOrWhiteSpace(awardId))
        {
            var paged = await retentions.GetPagedAsync(parameters);
            result = new PaginatedResult<PaymentRetentionReadDto>
            {
                Items = paged.Items.Select(ToReadDto).ToList(),
                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize,
            };
        }
        else
        {
            var filtered = await retentions.FindAsync(r => r.AwardId == awardId);
            result = new PaginatedResult<PaymentRetentionReadDto>
            {
                Items = filtered.Skip((parameters.Page - 1) * parameters.PageSize).Take(parameters.PageSize).Select(ToReadDto).ToList(),
                TotalCount = filtered.Count,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
            };
        }
        return Ok(ApiResponse<PaginatedResult<PaymentRetentionReadDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Policy = "subcontracts.write")]
    public async Task<ActionResult<ApiResponse<PaymentRetentionReadDto>>> Create([FromBody] CreatePaymentRetentionDto dto)
    {
        RetentionRules.Validate(dto.CertifiedAmount, dto.RetentionHeld, dto.Wht);

        var retention = new PaymentRetention
        {
            AwardId = dto.AwardId,
            CertifiedAmount = dto.CertifiedAmount,
            RetentionHeld = dto.RetentionHeld,
            Wht = dto.Wht,
        };
        await retentions.CreateAsync(retention);
        return Ok(ApiResponse<PaymentRetentionReadDto>.Ok(ToReadDto(retention), "Payment certified."));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "subcontracts.read")]
    public async Task<ActionResult<ApiResponse<PaymentRetentionReadDto>>> GetById(string id)
    {
        var retention = await retentions.GetByIdAsync(id);
        if (retention == null) return NotFound(ApiResponse<PaymentRetentionReadDto>.Fail("Payment record not found.", 404));
        return Ok(ApiResponse<PaymentRetentionReadDto>.Ok(ToReadDto(retention)));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "subcontracts.write")]
    public async Task<ActionResult<ApiResponse<PaymentRetentionReadDto>>> Update(string id, [FromBody] UpdatePaymentRetentionDto dto)
    {
        var retention = await retentions.GetByIdAsync(id);
        if (retention == null) return NotFound(ApiResponse<PaymentRetentionReadDto>.Fail("Payment record not found.", 404));

        // Validated on update too: the create guard alone would let a certificate be corrected into
        // a state it could never have been created in.
        RetentionRules.Validate(dto.CertifiedAmount, dto.RetentionHeld, dto.Wht);

        retention.CertifiedAmount = dto.CertifiedAmount;
        retention.RetentionHeld = dto.RetentionHeld;
        retention.Wht = dto.Wht;
        await retentions.UpdateAsync(retention);

        return Ok(ApiResponse<PaymentRetentionReadDto>.Ok(ToReadDto(retention), "Payment record updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "subcontracts.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await retentions.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "Payment record not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "Payment record deleted successfully.", StatusCode = 200 });
    }

    [HttpPatch("{id}/mark-paid")]
    [Authorize(Policy = "subcontracts.write")]
    public async Task<ActionResult<ApiResponse<PaymentRetentionReadDto>>> MarkPaid(string id)
    {
        var retention = await retentions.GetByIdAsync(id);
        if (retention == null) return NotFound(ApiResponse<PaymentRetentionReadDto>.Fail("Payment record not found.", 404));

        retention.Status = RetentionStatus.Paid;
        retention.PaidOn = DateTime.UtcNow;
        await retentions.UpdateAsync(retention);
        return Ok(ApiResponse<PaymentRetentionReadDto>.Ok(ToReadDto(retention), "Payment marked as paid."));
    }

    private static PaymentRetentionReadDto ToReadDto(PaymentRetention p) => new()
    {
        Id = p.Id,
        AwardId = p.AwardId,
        CertifiedAmount = p.CertifiedAmount,
        RetentionHeld = p.RetentionHeld,
        Wht = p.Wht,
        Status = p.Status.ToString(),
        PaidOn = p.PaidOn,
    };
}
