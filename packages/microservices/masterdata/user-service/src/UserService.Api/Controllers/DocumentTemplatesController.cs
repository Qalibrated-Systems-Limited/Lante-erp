using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

// Footer note / terms text overrides for generated PDFs. Read by any authenticated user (the
// export functions need it at PDF-generation time); writes are settings.manage only.
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/document-templates")]
public class DocumentTemplatesController(IDocumentTemplateService templateService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var templates = await templateService.GetAllAsync();
        return OkResult(templates);
    }

    [HttpGet("{docType}")]
    public async Task<IActionResult> GetByDocType(string docType)
    {
        var template = await templateService.GetByDocTypeAsync(docType);
        if (template == null) return NotFoundResult($"Unknown document type '{docType}'.");
        return OkResult(template);
    }

    [HttpPut("{docType}")]
    [Authorize(Policy = "settings.manage")]
    public async Task<IActionResult> Update(string docType, [FromBody] UpdateDocumentTemplateRequest request)
    {
        try
        {
            var updated = await templateService.UpsertAsync(docType, request.FooterNote, request.TermsText);
            return OkResult(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResult(ex.Message);
        }
    }

    public record UpdateDocumentTemplateRequest(string? FooterNote, string? TermsText);
}
