using AutoMapper;
using CrmService.Core.Entities;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Mappings;
using CrmService.Core.Services;
using CrmService.Infrastructure.Data;
using CrmService.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CrmService.Tests;

/// <summary>
/// Customer records, duplicate detection and credit limits, against a real relational database.
///
/// <para><b>SQLite, not EF InMemory.</b> The duplicate check is a four-clause OR with
/// <c>ToLower()</c> and <c>ToUpper()</c> applied on both sides of the comparison — precisely the kind
/// of expression whose translation an EF major version changes, and one that can silently start
/// evaluating client-side (pulling every customer into memory on each check). InMemory never builds
/// SQL, so it would assert nothing about any of that (#247, and the 8→9 bump in #227).</para>
///
/// <para><b>Limits, stated rather than implied.</b> SQLite is not Postgres: <c>ToLower()</c> maps to
/// SQLite's ASCII-only <c>lower()</c>, whereas Npgsql uses the database collation, so case handling
/// for non-ASCII names agrees here and could differ in production. No jsonb, and <c>decimal</c> is
/// stored by type affinity. A translation that works here and fails on Npgsql still gets through.
/// This is a large step up from no data-layer coverage, not a substitute for Testcontainers.</para>
///
/// <para>The connection is held open for the fixture's lifetime — an in-memory SQLite database is
/// destroyed when its last connection closes.</para>
/// </summary>
public sealed class CustomerFixture : IDisposable
{
    public const string Actor = "sales-1";

    private readonly SqliteConnection _conn;

    public CrmDbContext Db { get; }
    public CustomerService Customers { get; }

    public CustomerFixture()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        Db = new CrmDbContext(new DbContextOptionsBuilder<CrmDbContext>().UseSqlite(_conn).Options);
        Db.Database.EnsureCreated();

        // The real profile: a wrong map is a real defect, and a mocked IMapper would assert nothing.
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>()).CreateMapper();

        Customers = new CustomerService(
            Repo<Customer>(), Repo<CustomerContact>(), Repo<ClientCreditLimit>(), mapper);
    }

    private IGenericRepository<T> Repo<T>() where T : class => new GenericRepository<T>(Db);

    /// <summary>Inserts a customer directly, bypassing the service, to set up a pre-existing record.</summary>
    public Customer Seed(string name, string? email = null, string? phone = null, string? kraPin = null)
    {
        var c = new Customer
        {
            Name = name, Email = email, Phone = phone, KraPin = kraPin,
            CreatedBy = Actor,
        };
        Db.Customers.Add(c);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return c;
    }

    public void Dispose() { Db.Dispose(); _conn.Dispose(); }
}
