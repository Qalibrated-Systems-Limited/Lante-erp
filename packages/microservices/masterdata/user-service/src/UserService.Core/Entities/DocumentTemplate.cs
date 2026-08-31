namespace UserService.Core.Entities;

// Per-tenant customization of the footer note / terms text printed on generated PDFs — scoped
// deliberately narrow: layout, titles, and form codes for ISO-controlled documents (Requisition
// forms, Travel Voucher) are NOT here and stay fixed by design. DocType is a plain key (e.g.
// "ticket", "grn") matching whichever export function renders that PDF.
public class DocumentTemplate : BaseEntity
{
    public string DocType { get; set; } = string.Empty;
    public string? FooterNote { get; set; }
    public string? TermsText { get; set; }
}
