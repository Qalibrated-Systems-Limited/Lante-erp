using UserService.Core.DTOs.Documents;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;

namespace UserService.Core.Services;

public class DocumentTemplateService(IDocumentTemplateRepository repository) : IDocumentTemplateService
{
    // The only two PDF generators that actually have a free-text footer/terms area today
    // (utils/export.js's exportTicketPdf and exportGrnPdf). ISO-controlled forms (Requisition
    // A-1/A-2, Travel Voucher) deliberately aren't here — their layout is fixed by design.
    private static readonly (string DocType, string DisplayName)[] KnownDocTypes =
    [
        ("ticket", "Ticket PDF"),
        ("grn", "Goods Received Note (GRN)"),
    ];

    public async Task<IEnumerable<DocumentTemplateDto>> GetAllAsync()
    {
        var existing = (await repository.GetAllAsync()).ToDictionary(d => d.DocType);
        return KnownDocTypes.Select(known => existing.TryGetValue(known.DocType, out var t)
            ? ToDto(t, known.DisplayName)
            : new DocumentTemplateDto(known.DocType, known.DisplayName, null, null, false));
    }

    public async Task<DocumentTemplateDto?> GetByDocTypeAsync(string docType)
    {
        var known = KnownDocTypes.FirstOrDefault(k => k.DocType == docType);
        if (known.DocType == null) return null;
        var t = await repository.GetByDocTypeAsync(docType);
        return t == null ? new DocumentTemplateDto(docType, known.DisplayName, null, null, false) : ToDto(t, known.DisplayName);
    }

    public async Task<DocumentTemplateDto> UpsertAsync(string docType, string? footerNote, string? termsText)
    {
        var known = KnownDocTypes.FirstOrDefault(k => k.DocType == docType);
        if (known.DocType == null) throw new KeyNotFoundException($"Unknown document type '{docType}'.");

        var existing = await repository.GetByDocTypeAsync(docType);
        if (existing == null)
        {
            var created = new DocumentTemplate { DocType = docType, FooterNote = footerNote, TermsText = termsText };
            await repository.CreateAsync(created);
            return ToDto(created, known.DisplayName);
        }

        existing.FooterNote = footerNote;
        existing.TermsText = termsText;
        await repository.UpdateAsync(existing);
        return ToDto(existing, known.DisplayName);
    }

    private static DocumentTemplateDto ToDto(DocumentTemplate t, string displayName) =>
        new(t.DocType, displayName, t.FooterNote, t.TermsText, !string.IsNullOrEmpty(t.FooterNote) || !string.IsNullOrEmpty(t.TermsText));
}
