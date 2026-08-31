using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Icm;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// ICM-004..010: dual-ledger intercompany recharges posted under an ICSA. Unreconciled
// transactions fan out 30/45-day age alerts (see ComplianceAlertsBackgroundService); the
// RELATED_PARTY_TXN_LOG / COMP-010 IAS 24 disclosure register is this table read
// year-by-year, not a separate one.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-icm-transactions")]
public class IntercompanyTxnsController(
    IComplianceCrudService<IntercompanyTxn> txns,
    IComplianceCrudService<RelatedParty> parties,
    IComplianceCrudService<Icsa> agreements,
    IIntercompanyTxnRepository txnsRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<IntercompanyTxnReadDto>>>> GetAll([FromQuery] IntercompanyTxnFilterParameters parameters)
    {
        var paged = await txnsRepository.GetPagedAsync(parameters);
        var names = (await parties.GetAllAsync()).ToDictionary(p => p.Id, p => p.CompanyName);
        var result = new PaginatedResult<IntercompanyTxnReadDto>
        {
            Items = paged.Items.Select(t => ToReadDto(t, names)).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<IntercompanyTxnReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<IntercompanyTxnReadDto>>> GetById(string id)
    {
        var t = await txns.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<IntercompanyTxnReadDto>.Fail("Transaction not found.", 404));
        var names = (await parties.GetAllAsync()).ToDictionary(p => p.Id, p => p.CompanyName);
        return Ok(ApiResponse<IntercompanyTxnReadDto>.Ok(ToReadDto(t, names)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<IntercompanyTxnReadDto>>> Create([FromBody] CreateIntercompanyTxnDto dto)
    {
        var party = await parties.GetByIdAsync(dto.RelatedPartyId);
        if (party == null) return NotFound(ApiResponse<IntercompanyTxnReadDto>.Fail("Related party not found.", 404));
        var agreement = await agreements.GetByIdAsync(dto.IcsaId);
        if (agreement == null) return NotFound(ApiResponse<IntercompanyTxnReadDto>.Fail("Inter-company services agreement not found.", 404));

        var t = new IntercompanyTxn
        {
            IcsaId = dto.IcsaId,
            RelatedPartyId = dto.RelatedPartyId,
            Year = dto.Year,
            Amount = dto.Amount,
            QslLedgerRef = dto.QslLedgerRef,
            SisterLedgerRef = dto.SisterLedgerRef,
            PostedAt = dto.PostedAt ?? DateTime.UtcNow,
        };
        await txns.CreateAsync(t);
        var names = new Dictionary<string, string> { [party.Id] = party.CompanyName };
        return CreatedAtAction(nameof(GetById), new { id = t.Id, version = "1" },
            ApiResponse<IntercompanyTxnReadDto>.Ok(ToReadDto(t, names), "Intercompany transaction posted."));
    }

    [HttpPatch("{id}/reconcile")]
    [Authorize(Policy = "compliance.approve")]
    public async Task<ActionResult<ApiResponse<IntercompanyTxnReadDto>>> Reconcile(string id)
    {
        var t = await txns.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<IntercompanyTxnReadDto>.Fail("Transaction not found.", 404));

        t.ReconciledAt = DateTime.UtcNow;
        await txns.UpdateAsync(t);
        var names = (await parties.GetAllAsync()).ToDictionary(p => p.Id, p => p.CompanyName);
        return Ok(ApiResponse<IntercompanyTxnReadDto>.Ok(ToReadDto(t, names), "Marked as reconciled on both ledgers."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<IntercompanyTxnReadDto>>> Update(string id, [FromBody] UpdateIntercompanyTxnDto dto)
    {
        var t = await txns.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<IntercompanyTxnReadDto>.Fail("Transaction not found.", 404));
        var party = await parties.GetByIdAsync(dto.RelatedPartyId);
        if (party == null) return NotFound(ApiResponse<IntercompanyTxnReadDto>.Fail("Related party not found.", 404));
        var agreement = await agreements.GetByIdAsync(dto.IcsaId);
        if (agreement == null) return NotFound(ApiResponse<IntercompanyTxnReadDto>.Fail("Inter-company services agreement not found.", 404));

        t.IcsaId = dto.IcsaId;
        t.RelatedPartyId = dto.RelatedPartyId;
        t.Year = dto.Year;
        t.Amount = dto.Amount;
        t.QslLedgerRef = dto.QslLedgerRef;
        t.SisterLedgerRef = dto.SisterLedgerRef;
        t.PostedAt = dto.PostedAt;
        await txns.UpdateAsync(t);

        var names = new Dictionary<string, string> { [party.Id] = party.CompanyName };
        return Ok(ApiResponse<IntercompanyTxnReadDto>.Ok(ToReadDto(t, names), "Transaction updated."));
    }

    private static IntercompanyTxnReadDto ToReadDto(IntercompanyTxn t, Dictionary<string, string> names)
    {
        names.TryGetValue(t.RelatedPartyId, out var name);
        return new IntercompanyTxnReadDto
        {
            Id = t.Id,
            IcsaId = t.IcsaId,
            RelatedPartyId = t.RelatedPartyId,
            RelatedPartyName = name,
            Year = t.Year,
            Amount = t.Amount,
            QslLedgerRef = t.QslLedgerRef,
            SisterLedgerRef = t.SisterLedgerRef,
            PostedAt = t.PostedAt,
            ReconciledAt = t.ReconciledAt,
            AgeDays = (int)((t.ReconciledAt ?? DateTime.UtcNow) - t.PostedAt).TotalDays,
        };
    }
}
