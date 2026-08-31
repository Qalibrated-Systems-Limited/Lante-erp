using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.RelatedParty;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// COMP-010: all transactions with shareholders, directors, or affiliates flagged and
// reported. Scoped to the flag+report register in the requirement text — the fuller
// Inter-Company Services Agreement / dual-ledger recharge system is a separate,
// larger Finance-module feature and intentionally not built here.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-related-party")]
public class RelatedPartyTransactionsController(
    IComplianceCrudService<RelatedPartyTransaction> transactions,
    IRelatedPartyTransactionRepository transactionsRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<RelatedPartyTransactionReadDto>>>> GetAll([FromQuery] RelatedPartyTransactionFilterParameters parameters)
    {
        var paged = await transactionsRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<RelatedPartyTransactionReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<RelatedPartyTransactionReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<RelatedPartyTransactionReadDto>>> GetById(string id)
    {
        var t = await transactions.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<RelatedPartyTransactionReadDto>.Fail("Transaction not found.", 404));
        return Ok(ApiResponse<RelatedPartyTransactionReadDto>.Ok(ToReadDto(t)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<RelatedPartyTransactionReadDto>>> Create([FromBody] CreateRelatedPartyTransactionDto dto)
    {
        var t = new RelatedPartyTransaction
        {
            PartyName = dto.PartyName,
            RelationshipType = dto.RelationshipType,
            TransactionDate = dto.TransactionDate,
            Amount = dto.Amount,
            Description = dto.Description,
            Flagged = true,
        };
        await transactions.CreateAsync(t);
        return CreatedAtAction(nameof(GetById), new { id = t.Id, version = "1" },
            ApiResponse<RelatedPartyTransactionReadDto>.Ok(ToReadDto(t), "Related party transaction flagged."));
    }

    [HttpPatch("{id}/report")]
    [Authorize(Policy = "compliance.approve")]
    public async Task<ActionResult<ApiResponse<RelatedPartyTransactionReadDto>>> MarkReported(string id)
    {
        var t = await transactions.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<RelatedPartyTransactionReadDto>.Fail("Transaction not found.", 404));

        t.Reported = true;
        t.ReportedAt = DateTime.UtcNow;
        await transactions.UpdateAsync(t);
        return Ok(ApiResponse<RelatedPartyTransactionReadDto>.Ok(ToReadDto(t), "Marked as reported."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<RelatedPartyTransactionReadDto>>> Update(string id, [FromBody] UpdateRelatedPartyTransactionDto dto)
    {
        var t = await transactions.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<RelatedPartyTransactionReadDto>.Fail("Transaction not found.", 404));

        t.PartyName = dto.PartyName;
        t.RelationshipType = dto.RelationshipType;
        t.TransactionDate = dto.TransactionDate;
        t.Amount = dto.Amount;
        t.Description = dto.Description;
        await transactions.UpdateAsync(t);

        return Ok(ApiResponse<RelatedPartyTransactionReadDto>.Ok(ToReadDto(t), "Transaction updated."));
    }

    private static RelatedPartyTransactionReadDto ToReadDto(RelatedPartyTransaction t) => new()
    {
        Id = t.Id,
        PartyName = t.PartyName,
        RelationshipType = t.RelationshipType,
        TransactionDate = t.TransactionDate,
        Amount = t.Amount,
        Description = t.Description,
        Flagged = t.Flagged,
        Reported = t.Reported,
        ReportedAt = t.ReportedAt,
    };
}
