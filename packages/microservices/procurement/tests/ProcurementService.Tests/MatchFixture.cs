using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.Entities;
using ProcurementService.Core.Enums;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Core.Interfaces.Services;
using ProcurementService.Core.Mappings;
using ProcurementService.Core.Services;
using ProcurementService.Infrastructure.Data;
using ProcurementService.Infrastructure.Repositories;

namespace ProcurementService.Tests;

/// <summary>
/// Three-way matching against a real relational database.
///
/// <para><b>SQLite, deliberately, not EF InMemory.</b> InMemory is not a relational provider — it never
/// builds SQL, so it cannot catch a query-translation regression. That is precisely the risk in the
/// EF 8→9 bump this coverage exists to unblock (#227, #247): a `Contains`, `GroupBy` or
/// `CountAsync` that silently starts evaluating client-side, or stops translating at all. SQLite
/// executes real SQL against a schema built from the model, so those failures surface here.</para>
///
/// <para><b>What it still does not cover.</b> SQLite is not Postgres: no `jsonb`, different collation
/// and date semantics, and `decimal` is stored by type affinity rather than exactly. A translation
/// that works on SQLite and fails on Npgsql would pass here. That gap needs Testcontainers, and the
/// tenant `search_path` interceptor (#217) is untouched either way. This is a large step up from no
/// data-layer coverage at all, not a substitute for the real provider.</para>
///
/// <para>The connection is held open for the fixture's lifetime — an in-memory SQLite database is
/// destroyed when its last connection closes, so a per-operation connection would lose the schema
/// between calls.</para>
/// </summary>
public sealed class MatchFixture : IDisposable
{
    public const string PoId = "po-1";
    public const string PoNumber = "LPO-2026-0001";
    public const string Actor = "buyer-1";

    private readonly SqliteConnection _conn;

    public ProcurementDbContext Db { get; }
    public ThreeWayMatchService Matches { get; }
    public FakeInvoiceGateway Invoices { get; }
    public FakeVoucherGateway Vouchers { get; }

    public MatchFixture(
        decimal poTotal = 100_000m,
        PoReceiptStatus receipt = PoReceiptStatus.FullyReceived,
        PoStatus status = PoStatus.Issued)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        Db = new ProcurementDbContext(
            new DbContextOptionsBuilder<ProcurementDbContext>().UseSqlite(_conn).Options);
        Db.Database.EnsureCreated();

        Db.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = PoId, PoNumber = PoNumber, PrId = "pr-1",
            SupplierId = "sup-1", SupplierName = "Acme Supplies",
            TotalAmount = poTotal, Status = status, ReceiptStatus = receipt,
            ReceivedQty = receipt == PoReceiptStatus.FullyReceived ? 10m : 4m,
            CreatedBy = Actor,
        });
        Db.SaveChanges();

        Invoices = new FakeInvoiceGateway();
        Vouchers = new FakeVoucherGateway();

        // The real mapping profile, not a stub: a wrong map is a real defect and mocking IMapper would
        // assert nothing about it.
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>()).CreateMapper();

        Matches = new ThreeWayMatchService(
            Repo<ThreeWayMatch>(), Repo<MatchingException>(), Repo<PurchaseOrder>(),
            Repo<ProcurementAuditLog>(), Invoices, Vouchers, mapper);
    }

    private IGenericRepository<T> Repo<T>() where T : class => new GenericRepository<T>(Db);

    /// <summary>An invoice Finance has recorded against the LPO, net of tax.</summary>
    public void InvoiceFor(decimal subtotal, string invoiceNo = "SINV-001") =>
        Invoices.Invoice = new SupplierInvoiceRef(
            "si-1", invoiceNo, "sup-1", "Acme Supplies",
            Subtotal: subtotal, Total: subtotal * 1.16m, Balance: subtotal,
            Status: "Approved", MatchStatus: "Pending");

    public void Dispose() { Db.Dispose(); _conn.Dispose(); }
}

public sealed class FakeInvoiceGateway : IInvoiceGateway
{
    public SupplierInvoiceRef? Invoice { get; set; }
    public bool Reachable { get; set; } = true;
    public List<(string Id, string Status, string? Note)> WriteBacks { get; } = [];

    public Task<InvoiceLookup> FindByLpoAsync(string poNumber, CancellationToken ct = default) =>
        Task.FromResult(Reachable
            ? new InvoiceLookup(true, Invoice, Invoice is null ? "No invoice found." : "Found.")
            : new InvoiceLookup(false, null, "Finance is unreachable."));

    public Task<bool> SetMatchStatusAsync(string supplierInvoiceId, string status, string? note, CancellationToken ct = default)
    {
        WriteBacks.Add((supplierInvoiceId, status, note));
        return Task.FromResult(true);
    }
}

public sealed class FakeVoucherGateway : IPaymentVoucherGateway
{
    public List<(string InvoiceId, decimal? Amount, string PoNumber)> Raised { get; } = [];
    public bool Succeed { get; set; } = true;

    public Task<VoucherResult> RaiseAsync(string supplierInvoiceId, decimal? amount, string? bankAccountCode,
                                          string poNumber, CancellationToken ct = default)
    {
        if (!Succeed) return Task.FromResult(new VoucherResult(false, null, null, "Finance is unreachable."));
        Raised.Add((supplierInvoiceId, amount, poNumber));
        return Task.FromResult(new VoucherResult(true, $"pv-{Raised.Count}", $"PV-{Raised.Count:D4}", "Raised."));
    }

    public Task<VoucherResult> RaiseAdHocAsync(string payee, decimal amountKes, string reference, CancellationToken ct = default)
        => Task.FromResult(new VoucherResult(true, "pv-adhoc", "PV-ADHOC", "Raised."));
}
