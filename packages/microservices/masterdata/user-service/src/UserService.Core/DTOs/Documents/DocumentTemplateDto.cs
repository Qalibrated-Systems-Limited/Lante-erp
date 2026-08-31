namespace UserService.Core.DTOs.Documents;

public record DocumentTemplateDto(string DocType, string DisplayName, string? FooterNote, string? TermsText, bool IsCustomised);

public record UpdateDocumentTemplateDto(string? FooterNote, string? TermsText);
