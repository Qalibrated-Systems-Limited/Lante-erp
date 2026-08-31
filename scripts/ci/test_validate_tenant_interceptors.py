"""
Tests for validate_tenant_interceptors.py — builds real fixture .cs files under a temp directory
per scenario, exercising the actual text-based checks rather than mocking them.
Run with: python3 -m unittest scripts/ci/test_validate_tenant_interceptors.py
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from validate_tenant_interceptors import find_wiring_problems

GOOD_DYNAMIC = '''
namespace Whatever.Infrastructure.Data;
public partial class TenantDbConnectionInterceptor : DbConnectionInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? Resolve()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return null;
        var schema = ctx.User.FindFirst("schema")?.Value
                     ?? ctx.Request.Headers["X-Tenant-Schema"].FirstOrDefault();
        if (!string.IsNullOrEmpty(schema) && !SafeSchemaRegex().IsMatch(schema))
            schema = null;
        return schema;
    }
    private async Task ApplyAsync(DbConnection connection, CancellationToken ct)
    {
        var schema = Resolve();
        cmd.CommandText = string.IsNullOrEmpty(schema) || schema == "public"
            ? "SET search_path TO public" : $"SET search_path TO \\"{schema}\\"";
    }
    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex SafeSchemaRegex();
}
'''

GOOD_STATIC = '''
namespace Whatever.Infrastructure.Data;
public class TenantDbConnectionInterceptor : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        cmd.CommandText = "SET search_path TO public, licensing";
    }
}
'''


class TenantInterceptorChecksTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _write(self, service: str, content: str, filename: str = "TenantDbConnectionInterceptor.cs"):
        d = self.root / service / "src/Whatever.Infrastructure/Data"
        d.mkdir(parents=True, exist_ok=True)
        (d / filename).write_text(content)

    def test_good_dynamic_interceptor_has_no_problems(self):
        self._write("svc", GOOD_DYNAMIC)
        self.assertEqual(find_wiring_problems(self.root), [])

    def test_good_static_interceptor_has_no_problems(self):
        self._write("svc", GOOD_STATIC)
        self.assertEqual(find_wiring_problems(self.root), [])

    def test_header_before_claim_is_caught(self):
        # The exact regression this whole check exists to prevent: header wins over JWT claim.
        bad = GOOD_DYNAMIC.replace(
            'var schema = ctx.User.FindFirst("schema")?.Value\n'
            '                     ?? ctx.Request.Headers["X-Tenant-Schema"].FirstOrDefault();',
            'var schema = ctx.Request.Headers["X-Tenant-Schema"].FirstOrDefault()\n'
            '                     ?? ctx.User.FindFirst("schema")?.Value;',
        )
        self._write("svc", bad)
        problems = find_wiring_problems(self.root)
        self.assertTrue(any("BEFORE the JWT schema claim" in p for p in problems))

    def test_weakened_regex_is_caught(self):
        bad = GOOD_DYNAMIC.replace(
            '[GeneratedRegex("^[a-z_][a-z0-9_]*$")]',
            '[GeneratedRegex(".*")]',
        )
        self._write("svc", bad)
        problems = find_wiring_problems(self.root)
        self.assertTrue(any("safe-schema-name regex" in p for p in problems))

    def test_missing_claim_lookup_is_caught(self):
        bad = GOOD_DYNAMIC.replace('ctx.User.FindFirst("schema")?.Value\n                     ?? ', "")
        self._write("svc", bad)
        problems = find_wiring_problems(self.root)
        self.assertTrue(any("missing the JWT 'schema' claim lookup" in p for p in problems))

    def test_missing_header_fallback_is_caught(self):
        bad = GOOD_DYNAMIC.replace(
            '\n                     ?? ctx.Request.Headers["X-Tenant-Schema"].FirstOrDefault()', ""
        )
        self._write("svc", bad)
        problems = find_wiring_problems(self.root)
        self.assertTrue(any("missing the X-Tenant-Schema header fallback" in p for p in problems))

    def test_validation_after_use_is_caught(self):
        # Swap the order so the interpolated SQL text appears earlier in the file than IsMatch —
        # simulates "validate after already using the value". Keep an HttpContext reference so
        # this still classifies as a "dynamic" interceptor, not the static/hardcoded variant.
        bad = (
            'namespace Whatever.Infrastructure.Data;\n'
            'private readonly IHttpContextAccessor _httpContextAccessor;\n'
            'var unused = $"SET search_path TO \\"{schema}\\", public";\n'
            'SafeSchemaRegex().IsMatch(schema);\n'
        )
        self._write("svc", bad)
        problems = find_wiring_problems(self.root)
        self.assertTrue(any("before the regex validation" in p for p in problems))

    def test_trailing_fallback_after_schema_is_caught(self):
        # #217: the exact regression this whole check exists to prevent — a missing tenant table
        # must fail loudly, not silently resolve into a trailing fallback schema still on the path.
        bad = GOOD_DYNAMIC.replace(
            '? "SET search_path TO public" : $"SET search_path TO \\"{schema}\\"";',
            '? "SET search_path TO public" : $"SET search_path TO \\"{schema}\\", public";',
        )
        self._write("svc", bad)
        problems = find_wiring_problems(self.root)
        self.assertTrue(any("trailing fallback schema" in p for p in problems))

    def test_userservice_differently_named_file_is_discovered(self):
        self._write("masterdata/user-service", GOOD_DYNAMIC, filename="UserServiceTenantConnectionInterceptor.cs")
        self.assertEqual(find_wiring_problems(self.root), [])

    def test_static_interceptor_with_interpolation_is_caught(self):
        # No HttpContext reference (classified static) but builds SQL from a variable anyway —
        # exactly the kind of half-refactored state that would be a real bug.
        bad = GOOD_STATIC.replace(
            'cmd.CommandText = "SET search_path TO public, licensing";',
            'cmd.CommandText = $"SET search_path TO public, {somethingElse}";',
        )
        self._write("svc", bad)
        problems = find_wiring_problems(self.root)
        self.assertTrue(any("must never build its search_path from anything request-derived" in p for p in problems))


if __name__ == "__main__":
    unittest.main()
