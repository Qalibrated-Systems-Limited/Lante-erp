using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using TicketingService.Infrastructure.Data;
using Xunit;

namespace TicketingService.Tests;

/// <summary>
/// This is the single point of failure for schema-per-tenant isolation across every service that
/// shares this interceptor (compliance/hse/reporting/subcontracts/stores/licensing/operations/
/// fleet/user-service are all copy-pasted from TicketingService's original — see each file's own
/// header comment). A bug here doesn't just misroute one query — it can leak one company's data
/// into another's connection, or let a caller repoint search_path via a client-suppliable header.
/// These tests exercise the REAL Apply()/Resolve() logic (via reflection, since both are private)
/// against a fake DbConnection that records the exact SQL text sent to Postgres, rather than
/// re-implementing the logic in the test and asserting against a copy of itself.
/// </summary>
public class TenantDbConnectionInterceptorTests
{
    private sealed class FakeDbParameterCollection : DbParameterCollection
    {
        private readonly List<object> _items = new();
        public override int Count => _items.Count;
        public override object SyncRoot => this;
        public override int Add(object value) { _items.Add(value); return _items.Count - 1; }
        public override void AddRange(Array values) => _items.AddRange(values.Cast<object>());
        public override void Clear() => _items.Clear();
        public override bool Contains(string value) => false;
        public override bool Contains(object value) => _items.Contains(value);
        public override void CopyTo(Array array, int index) => _items.CopyTo((object[])array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(string parameterName) => -1;
        public override int IndexOf(object value) => _items.IndexOf(value);
        public override void Insert(int index, object value) => _items.Insert(index, value);
        public override void Remove(object value) => _items.Remove(value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) { }
        protected override DbParameter GetParameter(int index) => (DbParameter)_items[index];
        protected override DbParameter GetParameter(string parameterName) => throw new NotImplementedException();
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => throw new NotImplementedException();
    }

    private sealed class FakeDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull]
        public override string ParameterName { get; set; } = "";
        [AllowNull]
        public override string SourceColumn { get; set; } = "";
        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }

    /// <summary>Records the CommandText of every command executed against it — the actual
    /// assertion surface: "what SQL would really be sent to Postgres for this request".</summary>
    private sealed class FakeDbCommand : DbCommand
    {
        public string? LastExecutedCommandText;
        [AllowNull]
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery() { LastExecutedCommandText = CommandText; return 0; }
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            LastExecutedCommandText = CommandText;
            return Task.FromResult(0);
        }
        public override object? ExecuteScalar() { LastExecutedCommandText = CommandText; return null; }
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
    }

    private sealed class FakeDbConnection : DbConnection
    {
        public FakeDbCommand? LastCommand;
        [AllowNull]
        public override string ConnectionString { get; set; } = "";
        public override string Database => "fake";
        public override string DataSource => "fake";
        public override string ServerVersion => "fake";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();
        protected override DbCommand CreateDbCommand()
        {
            LastCommand = new FakeDbCommand();
            return LastCommand;
        }
    }

    /// <summary>Builds a real interceptor wired to an HttpContext with the given JWT `schema`
    /// claim and/or X-Tenant-Schema header, then invokes its private Apply(DbConnection) — the
    /// exact method EF Core calls on every connection open — via reflection, and returns the SQL
    /// text that was actually sent.</summary>
    private const string ServiceKey = "test-internal-key";

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["InternalServices:ServiceKey"] = ServiceKey })
            .Build();

    private static string ApplyAndCaptureSql(
        string? schemaClaim = null, string? schemaHeader = null, bool noHttpContext = false,
        bool validInternalKey = false, string? resolvedItemsSchema = null)
    {
        IHttpContextAccessor accessor;
        if (noHttpContext)
        {
            var mock = new Mock<IHttpContextAccessor>();
            mock.Setup(a => a.HttpContext).Returns((HttpContext?)null);
            accessor = mock.Object;
        }
        else
        {
            var httpContext = new DefaultHttpContext();
            var claims = new List<Claim>();
            if (schemaClaim != null) claims.Add(new Claim("schema", schemaClaim));
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            if (schemaHeader != null) httpContext.Request.Headers["X-Tenant-Schema"] = schemaHeader;
            if (validInternalKey) httpContext.Request.Headers["X-Internal-Key"] = ServiceKey;
            if (resolvedItemsSchema != null) httpContext.Items["ResolvedTenantSchema"] = resolvedItemsSchema;

            var mock = new Mock<IHttpContextAccessor>();
            mock.Setup(a => a.HttpContext).Returns(httpContext);
            accessor = mock.Object;
        }

        var interceptor = new TenantDbConnectionInterceptor(accessor, BuildConfiguration());
        var connection = new FakeDbConnection();

        var applyMethod = typeof(TenantDbConnectionInterceptor)
            .GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(DbConnection) })!;
        applyMethod.Invoke(interceptor, new object[] { connection });

        return connection.LastCommand!.LastExecutedCommandText!;
    }

    [Fact]
    public void ValidJwtSchemaClaim_SetsSearchPathToThatSchemaOnly()
    {
        // #217: a resolved tenant schema is the ONLY thing on search_path — no trailing `public`
        // fallback, so a table missing from the tenant schema fails loudly instead of silently
        // resolving into a stale/shared public copy.
        ApplyAndCaptureSql(schemaClaim: "tenant_qsl")
            .Should().Be("SET search_path TO \"tenant_qsl\"");
    }

    [Fact]
    public void ValidHeaderWithValidInternalKey_UsedAsFallbackForNoJwtClaim()
    {
        // The documented use case: internal/service-key calls have no ASP.NET user identity at all,
        // but must prove they're a trusted internal caller via X-Internal-Key.
        ApplyAndCaptureSql(schemaHeader: "tenant_qsl", validInternalKey: true)
            .Should().Be("SET search_path TO \"tenant_qsl\"");
    }

    [Fact]
    public void HeaderWithoutValidInternalKey_IsNotTrustedAndFallsBackToPublic()
    {
        // The critical security property: anonymous [AllowAnonymous] callers (e.g. the public
        // portal) can set arbitrary headers on their own request. Without a valid X-Internal-Key,
        // the header must never be trusted — otherwise any anonymous caller could repoint their
        // connection at another tenant's schema just by setting X-Tenant-Schema.
        ApplyAndCaptureSql(schemaHeader: "tenant_qsl")
            .Should().Be("SET search_path TO public");
    }

    [Fact]
    public void JwtClaimTakesPrecedenceOverHeader_WhenBothPresent()
    {
        // The critical security property: an authenticated user's own JWT is the only trustworthy
        // source of their tenant. If the header ever won here, any authenticated user could
        // repoint their own connection at another tenant's schema just by setting X-Tenant-Schema.
        ApplyAndCaptureSql(schemaClaim: "tenant_qsl", schemaHeader: "tenant_someone_else", validInternalKey: true)
            .Should().Be("SET search_path TO \"tenant_qsl\"");
    }

    [Fact]
    public void ResolvedItemsSchema_TrustedWhenNoJwtClaim()
    {
        // The anonymous public-portal path: PortalTenantMiddleware validates the request's ?slug=
        // against user-service and stamps the result onto HttpContext.Items server-side — safe to
        // trust as-is since Items is never client-settable (unlike the header above).
        ApplyAndCaptureSql(resolvedItemsSchema: "tenant_qsl")
            .Should().Be("SET search_path TO \"tenant_qsl\"");
    }

    [Fact]
    public void JwtClaimTakesPrecedenceOverResolvedItemsSchema_WhenBothPresent()
    {
        ApplyAndCaptureSql(schemaClaim: "tenant_qsl", resolvedItemsSchema: "tenant_someone_else")
            .Should().Be("SET search_path TO \"tenant_qsl\"");
    }

    [Fact]
    public void NoClaimAndNoHeader_FallsBackToPublicOnly()
    {
        ApplyAndCaptureSql().Should().Be("SET search_path TO public");
    }

    [Fact]
    public void NoHttpContext_FallsBackToPublicOnly()
    {
        // Background services / startup migration code run outside a request scope entirely.
        ApplyAndCaptureSql(noHttpContext: true).Should().Be("SET search_path TO public");
    }

    [Fact]
    public void LiteralPublicClaim_DoesNotDoubleTheSchema()
    {
        ApplyAndCaptureSql(schemaClaim: "public").Should().Be("SET search_path TO public");
    }

    [Theory]
    // Each of these is a plausible attempt to break out of the quoted identifier and inject
    // arbitrary SQL via the schema claim/header — every one MUST be rejected (silently falls back
    // to public), because the value is interpolated directly into `SET search_path TO "{schema}"`.
    [InlineData("tenant_x\"; DROP TABLE users; --")]
    [InlineData("tenant_x\", pg_catalog")]
    [InlineData("tenant_x'; SELECT 1; --")]
    [InlineData("public\", tenant_someone_else")]
    [InlineData("Tenant_Qsl")]           // uppercase not allowed by the identifier regex
    [InlineData("1tenant")]              // can't start with a digit
    [InlineData("tenant qsl")]           // space
    [InlineData("tenant-qsl")]           // hyphen
    [InlineData("")]                    // empty string is falsy, but exercise it explicitly
    public void MaliciousOrMalformedSchema_IsRejectedAndFallsBackToPublic(string maliciousSchema)
    {
        ApplyAndCaptureSql(schemaClaim: maliciousSchema)
            .Should().Be("SET search_path TO public");
    }

    [Theory]
    [InlineData("tenant_qsl")]
    [InlineData("tenant_acme_2")]
    [InlineData("_leading_underscore")]
    [InlineData("public")]
    public void WellFormedSchemaNames_AreAccepted(string schema)
    {
        var expected = schema == "public"
            ? "SET search_path TO public"
            : $"SET search_path TO \"{schema}\"";
        ApplyAndCaptureSql(schemaClaim: schema).Should().Be(expected);
    }
}
