using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>O5 — a quotation raised against a service request. Migrated from ticketing.</summary>
public class Quotation : BaseEntity
{
    public string ServiceRequestId  { get; set; } = string.Empty;
    public string QuotationNumber   { get; set; } = string.Empty;  // QT-2026-0001
    public QuotationStatus Status   { get; set; } = QuotationStatus.Draft;

    public DateTime? ValidUntil     { get; set; }

    // JSON: [{description, quantity, unitPrice, amount}]
    public string LineItemsJson     { get; set; } = "[]";

    public decimal Subtotal         { get; set; }
    public decimal VatRate          { get; set; } = 0.16m;  // 16% VAT
    public decimal VatAmount        { get; set; }
    public decimal TotalAmount      { get; set; }

    public string? Notes            { get; set; }
    public DateTime? SentAt         { get; set; }

    // LPO tracking
    public string?   LpoNumber      { get; set; }
    public DateTime? LpoReceivedAt  { get; set; }
    public DateTime? AcceptedAt     { get; set; }
    public DateTime? RejectedAt     { get; set; }

    // Navigation
    public ServiceRequest ServiceRequest { get; set; } = null!;
}
