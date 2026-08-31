"""
Tests for validate_audit_interceptor_coverage.py — fixture files in a temp directory, matching the
convention of the other validators here. Run with:
python3 -m unittest test_validate_audit_interceptor_coverage -v
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

import validate_audit_interceptor_coverage as v


class ServiceOfTests(unittest.TestCase):
    def test_direct_service(self):
        self.assertEqual(v.service_of(
            "/repo/packages/microservices/finance/src/FinanceService.Infrastructure/Data/x.cs"),
            "finance")

    def test_nested_masterdata_service(self):
        self.assertEqual(v.service_of(
            "/repo/packages/microservices/masterdata/user-service/src/UserService.Infrastructure/x.cs"),
            "user-service")


class CoverageDetectionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _write(self, rel_path: str, body: str) -> None:
        p = self.root / rel_path
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(body, encoding="utf-8")

    def test_a_service_with_a_SaveChangesInterceptor_subclass_is_covered(self):
        self._write("packages/microservices/finance/src/FinanceService.Infrastructure/Data/FinanceAuditInterceptor.cs",
                     "public class FinanceAuditInterceptor(IHttpContextAccessor a) : SaveChangesInterceptor { }")
        self._write("packages/microservices/crm/src/CrmService.Infrastructure/Data/CrmDbContext.cs", "public class CrmDbContext { }")
        self.assertEqual(v.covered_services(str(self.root)), {"finance"})

    def test_a_service_with_no_interceptor_class_is_not_covered(self):
        self._write("packages/microservices/crm/src/CrmService.Infrastructure/Data/CrmDbContext.cs", "public class CrmDbContext { }")
        self.assertEqual(v.covered_services(str(self.root)), set())

    def test_an_interceptor_class_in_the_service_tests_folder_does_not_count(self):
        # Coverage means the SERVICE ships the pattern, not that a test fixture happens to construct one
        # inline — though in practice the real interceptor lives in src/ and tests reference it.
        self._write("packages/microservices/crm/tests/CrmService.Tests/FakeInterceptor.cs",
                     "public class FakeInterceptor : SaveChangesInterceptor { }")
        self.assertEqual(v.covered_services(str(self.root)), set())

    def test_all_services_enumerates_every_src_directory(self):
        self._write("packages/microservices/finance/src/FinanceService.Core/x.cs", "// x")
        self._write("packages/microservices/crm/src/CrmService.Core/x.cs", "// x")
        self._write("packages/microservices/masterdata/user-service/src/UserService.Core/x.cs", "// x")
        self.assertEqual(v.all_services(str(self.root)), {"finance", "crm", "user-service"})

    def test_finding_zero_services_is_a_hard_failure(self):
        # Mirrors validate_dead_entities.py's parse_dbsets guard: a checker that finds nothing and
        # reports success is the bug this exists to prevent.
        with self.assertRaises(SystemExit) as ctx:
            v.all_services(str(self.root))
        self.assertEqual(ctx.exception.code, 2)


class RealRepoTests(unittest.TestCase):
    def test_the_baseline_matches_reality(self):
        services = v.all_services()
        covered = v.covered_services()
        gaps = services - covered
        self.assertEqual(gaps, set(v.KNOWN_GAPS),
                         f"untracked: {sorted(gaps - set(v.KNOWN_GAPS))}, "
                         f"stale: {sorted(set(v.KNOWN_GAPS) - gaps)}")

    def test_all_ten_reference_implementations_are_covered(self):
        # The reference implementations found (finance) or added (stores, crm, fleet-service,
        # compliance, hse, licensing, subcontracts, ticketing, operations). If any regresses out of
        # coverage, the baseline test above already catches it — this pins the reason.
        covered = v.covered_services()
        for svc in ("finance", "stores", "crm", "fleet-service", "compliance", "hse",
                    "licensing", "subcontracts", "ticketing", "operations"):
            self.assertIn(svc, covered)

    def test_the_sweep_actually_sees_the_repo(self):
        # Guards against a glob or path change silently reducing coverage to a handful.
        self.assertGreaterEqual(len(v.all_services()), 10)


if __name__ == "__main__":
    unittest.main()
