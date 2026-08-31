using Asp.Versioning;
using StoreService.Core.DTOs.Issues;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/store-issues")]
public class StoreIssueNotesController(IStockService stock) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] StoreIssueNoteFilterParameters filters)
    {
        var result = await stock.GetStoreIssuesAsync(filters);
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var issue = await stock.GetStoreIssueByIdAsync(id);
        if (issue is null) return NotFoundResult($"Store issue note {id} not found.");
        return OkResult(issue);
    }

    [HttpPost]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Create([FromBody] CreateStoreIssueNoteDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        try
        {
            var issue = await stock.CreateStoreIssueAsync(dto, CurrentUserId);
            return CreatedResult(issue, "Store issue note created.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
        catch (InvalidOperationException e)
        {
            return BadRequestResult(e.Message);
        }
    }
}
