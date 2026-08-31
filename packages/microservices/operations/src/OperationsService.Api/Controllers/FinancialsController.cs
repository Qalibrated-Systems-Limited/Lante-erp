using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Claims;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Financial;
using OperationsService.Core.DTOs.Requisitions;
using OperationsService.Core.Interfaces.Services;
using System.Security.Claims;

namespace OperationsService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/financials")]
[Authorize]
public class FinancialsController(IFinancialService financialService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;

    // ── Requisitions ──────────────────────────────────────────────────────────

    [HttpGet("requisitions")]
    public async Task<ActionResult<ApiResponse<IEnumerable<RequisitionReadDto>>>> GetRequisitions([FromQuery] RequisitionFilterParameters filters)
    {
        var items = await financialService.GetRequisitionsAsync(filters);
        return Ok(new ApiResponse<IEnumerable<RequisitionReadDto>> { Success = true, Data = items });
    }

    [HttpGet("requisitions/{id:guid}")]
    public async Task<ActionResult<ApiResponse<RequisitionReadDto>>> GetRequisition(Guid id)
    {
        var item = await financialService.GetRequisitionByIdAsync(id.ToString());
        if (item is null) return NotFound(new ApiResponse<RequisitionReadDto> { Success = false, Message = "Not found." });
        return Ok(new ApiResponse<RequisitionReadDto> { Success = true, Data = item });
    }

    [HttpPost("requisitions")]
    public async Task<ActionResult<ApiResponse<RequisitionReadDto>>> CreateRequisition([FromBody] CreateRequisitionDto dto)
    {
        var item = await financialService.CreateRequisitionAsync(dto, UserId, UserName);
        return Ok(new ApiResponse<RequisitionReadDto> { Success = true, Data = item });
    }

    [HttpPut("requisitions/{id:guid}")]
    public async Task<ActionResult<ApiResponse<RequisitionReadDto>>> UpdateRequisition(Guid id, [FromBody] UpdateRequisitionDto dto)
    {
        var item = await financialService.UpdateRequisitionAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<RequisitionReadDto> { Success = true, Data = item });
    }

    [HttpPost("requisitions/{id:guid}/review/manager")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<RequisitionReadDto>>> ReviewRequisitionByManager(Guid id, [FromBody] ReviewRequisitionDto dto)
    {
        var item = await financialService.ReviewRequisitionByManagerAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<RequisitionReadDto> { Success = true, Data = item });
    }

    [HttpPost("requisitions/{id:guid}/review/cfo")]
    [Authorize(Policy = "Permission:finance.approve")]
    public async Task<ActionResult<ApiResponse<RequisitionReadDto>>> ReviewRequisitionByCfo(Guid id, [FromBody] ReviewRequisitionDto dto)
    {
        var item = await financialService.ReviewRequisitionByCfoAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<RequisitionReadDto> { Success = true, Data = item });
    }

    [HttpPost("requisitions/{id:guid}/paid")]
    [Authorize(Policy = "Permission:finance.approve")]
    public async Task<ActionResult<ApiResponse<RequisitionReadDto>>> MarkPaid(Guid id)
    {
        var item = await financialService.MarkRequisitionPaidAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<RequisitionReadDto> { Success = true, Data = item });
    }

    // ── Claims ────────────────────────────────────────────────────────────────

    [HttpGet("claims")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ClaimReadDto>>>> GetClaims([FromQuery] ClaimFilterParameters filters)
    {
        var items = await financialService.GetClaimsAsync(filters);
        return Ok(new ApiResponse<IEnumerable<ClaimReadDto>> { Success = true, Data = items });
    }

    [HttpGet("claims/{id:guid}")]
    public async Task<ActionResult<ApiResponse<ClaimReadDto>>> GetClaim(Guid id)
    {
        var item = await financialService.GetClaimByIdAsync(id.ToString());
        if (item is null) return NotFound(new ApiResponse<ClaimReadDto> { Success = false, Message = "Not found." });
        return Ok(new ApiResponse<ClaimReadDto> { Success = true, Data = item });
    }

    [HttpPost("claims")]
    public async Task<ActionResult<ApiResponse<ClaimReadDto>>> CreateClaim([FromBody] CreateClaimDto dto)
    {
        var item = await financialService.CreateClaimAsync(dto, UserId, UserName);
        return Ok(new ApiResponse<ClaimReadDto> { Success = true, Data = item });
    }

    [HttpPost("claims/{id:guid}/review/manager")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<ClaimReadDto>>> ReviewClaimByManager(Guid id, [FromBody] ReviewClaimDto dto)
    {
        var item = await financialService.ReviewClaimByManagerAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<ClaimReadDto> { Success = true, Data = item });
    }

    [HttpPost("claims/{id:guid}/review/cfo")]
    [Authorize(Policy = "Permission:finance.approve")]
    public async Task<ActionResult<ApiResponse<ClaimReadDto>>> ReviewClaimByCfo(Guid id, [FromBody] ReviewClaimDto dto)
    {
        var item = await financialService.ReviewClaimByCfoAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<ClaimReadDto> { Success = true, Data = item });
    }

    [HttpPost("claims/{id:guid}/disburse")]
    [Authorize(Policy = "Permission:finance.approve")]
    public async Task<ActionResult<ApiResponse<ClaimReadDto>>> DisbursesClaim(Guid id)
    {
        var item = await financialService.MarkClaimDisbursedAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<ClaimReadDto> { Success = true, Data = item });
    }

    // ── Petty Cash ────────────────────────────────────────────────────────────

    [HttpPost("petty-cash")]
    public async Task<ActionResult<ApiResponse<PettyCashReadDto>>> CreatePettyCash([FromBody] CreatePettyCashAdvanceDto dto)
    {
        var item = await financialService.CreatePettyCashAsync(dto, UserId, UserName);
        return Ok(new ApiResponse<PettyCashReadDto> { Success = true, Data = item });
    }

    [HttpGet("petty-cash/assignment/{assignmentId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<PettyCashReadDto>>>> GetPettyCash(Guid assignmentId)
    {
        var items = await financialService.GetPettyCashByAssignmentAsync(assignmentId.ToString());
        return Ok(new ApiResponse<IEnumerable<PettyCashReadDto>> { Success = true, Data = items });
    }

    [HttpPost("petty-cash/{id:guid}/review/manager")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<PettyCashReadDto>>> ReviewPettyCashByManager(Guid id, [FromBody] ReviewPettyCashDto dto)
    {
        var item = await financialService.ReviewPettyCashByManagerAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<PettyCashReadDto> { Success = true, Data = item });
    }

    [HttpPost("petty-cash/{id:guid}/review/cfo")]
    [Authorize(Policy = "Permission:finance.approve")]
    public async Task<ActionResult<ApiResponse<PettyCashReadDto>>> ReviewPettyCashByCfo(Guid id, [FromBody] ReviewPettyCashDto dto)
    {
        var item = await financialService.ReviewPettyCashByCfoAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<PettyCashReadDto> { Success = true, Data = item });
    }

    [HttpPost("petty-cash/{id:guid}/disburse")]
    [Authorize(Policy = "Permission:finance.approve")]
    public async Task<ActionResult<ApiResponse<PettyCashReadDto>>> DisbursePettyCash(Guid id)
    {
        var item = await financialService.MarkPettyCashDisbursedAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<PettyCashReadDto> { Success = true, Data = item });
    }

    // ── Per Diem ──────────────────────────────────────────────────────────────

    [HttpPost("per-diem")]
    public async Task<ActionResult<ApiResponse<PerDiemReturnReadDto>>> CreatePerDiem([FromBody] CreatePerDiemReturnDto dto)
    {
        var item = await financialService.CreatePerDiemReturnAsync(dto, UserId, UserName);
        return Ok(new ApiResponse<PerDiemReturnReadDto> { Success = true, Data = item });
    }

    [HttpGet("per-diem/assignment/{assignmentId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<PerDiemReturnReadDto>>>> GetPerDiem(Guid assignmentId)
    {
        var items = await financialService.GetPerDiemReturnsByAssignmentAsync(assignmentId.ToString());
        return Ok(new ApiResponse<IEnumerable<PerDiemReturnReadDto>> { Success = true, Data = items });
    }

    [HttpPost("per-diem/{id:guid}/review/manager")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<PerDiemReturnReadDto>>> ReviewPerDiemByManager(Guid id, [FromBody] ReviewPerDiemReturnDto dto)
    {
        var item = await financialService.ReviewPerDiemReturnByManagerAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<PerDiemReturnReadDto> { Success = true, Data = item });
    }

    [HttpPost("per-diem/{id:guid}/review/cfo")]
    [Authorize(Policy = "Permission:finance.approve")]
    public async Task<ActionResult<ApiResponse<PerDiemReturnReadDto>>> ReviewPerDiemByCfo(Guid id, [FromBody] ReviewPerDiemReturnDto dto)
    {
        var item = await financialService.ReviewPerDiemReturnByCfoAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<PerDiemReturnReadDto> { Success = true, Data = item });
    }

    // ── Advance Returns ───────────────────────────────────────────────────────

    [HttpPost("advance-returns")]
    public async Task<ActionResult<ApiResponse<AdvanceReturnReadDto>>> CreateAdvanceReturn([FromBody] CreateAdvanceReturnDto dto)
    {
        var item = await financialService.CreateAdvanceReturnAsync(dto, UserId, UserName);
        return Ok(new ApiResponse<AdvanceReturnReadDto> { Success = true, Data = item });
    }

    [HttpGet("advance-returns/assignment/{assignmentId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AdvanceReturnReadDto>>>> GetAdvanceReturns(Guid assignmentId)
    {
        var items = await financialService.GetAdvanceReturnsByAssignmentAsync(assignmentId.ToString());
        return Ok(new ApiResponse<IEnumerable<AdvanceReturnReadDto>> { Success = true, Data = items });
    }

    [HttpPost("advance-returns/{id:guid}/review/manager")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<AdvanceReturnReadDto>>> ReviewAdvanceReturnByManager(Guid id, [FromBody] ReviewAdvanceReturnDto dto)
    {
        var item = await financialService.ReviewAdvanceReturnByManagerAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<AdvanceReturnReadDto> { Success = true, Data = item });
    }

    [HttpPost("advance-returns/{id:guid}/review/cfo")]
    [Authorize(Policy = "Permission:finance.approve")]
    public async Task<ActionResult<ApiResponse<AdvanceReturnReadDto>>> ReviewAdvanceReturnByCfo(Guid id, [FromBody] ReviewAdvanceReturnDto dto)
    {
        var item = await financialService.ReviewAdvanceReturnByCfoAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<AdvanceReturnReadDto> { Success = true, Data = item });
    }

    // ── Refunds ───────────────────────────────────────────────────────────────

    [HttpPost("refunds")]
    public async Task<ActionResult<ApiResponse<RefundReadDto>>> CreateRefund([FromBody] CreateRefundDto dto)
    {
        var item = await financialService.CreateRefundAsync(dto, UserId, UserName);
        return Ok(new ApiResponse<RefundReadDto> { Success = true, Data = item });
    }

    [HttpGet("refunds/assignment/{assignmentId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<RefundReadDto>>>> GetRefunds(Guid assignmentId)
    {
        var items = await financialService.GetRefundsByAssignmentAsync(assignmentId.ToString());
        return Ok(new ApiResponse<IEnumerable<RefundReadDto>> { Success = true, Data = items });
    }

    [HttpPost("refunds/{id:guid}/review")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<RefundReadDto>>> ReviewRefund(Guid id, [FromBody] ReviewRefundDto dto)
    {
        var item = await financialService.ReviewRefundAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<RefundReadDto> { Success = true, Data = item });
    }

    [HttpPost("refunds/{id:guid}/process")]
    [Authorize(Policy = "Permission:finance.approve")]
    public async Task<ActionResult<ApiResponse<RefundReadDto>>> ProcessRefund(Guid id)
    {
        var item = await financialService.ProcessRefundAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<RefundReadDto> { Success = true, Data = item });
    }
}
