using UserService.Core.DTOs.Documents;

namespace UserService.Core.Interfaces.Services;

public interface IDocumentTemplateService
{
    Task<IEnumerable<DocumentTemplateDto>> GetAllAsync();
    Task<DocumentTemplateDto?> GetByDocTypeAsync(string docType);
    Task<DocumentTemplateDto> UpsertAsync(string docType, string? footerNote, string? termsText);
}
