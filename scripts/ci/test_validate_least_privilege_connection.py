"""
Tests for validate_least_privilege_connection.py — builds real fixture files under a temp directory
for each scenario rather than mocking. Run with: python3 -m unittest test_validate_least_privilege_connection
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from validate_least_privilege_connection import find_problems


class LeastPrivilegeConnectionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _write_interceptor(self, service_dir: str, filename: str = "TenantDbConnectionInterceptor.cs"):
        data_dir = self.root / service_dir / "src/SomeInfra/Data"
        data_dir.mkdir(parents=True, exist_ok=True)
        (data_dir / filename).write_text("public class TenantDbConnectionInterceptor {}")

    def _write_app_connection_reference(self, service_dir: str, rel_path: str = "src/SomeApi/Program.cs"):
        f = self.root / service_dir / rel_path
        f.parent.mkdir(parents=True, exist_ok=True)
        f.write_text('var x = config.GetConnectionString("AppConnection") ?? config.GetConnectionString("DefaultConnection");')

    def test_service_with_app_connection_has_no_problems(self):
        self._write_interceptor("finance")
        self._write_app_connection_reference("finance")

        self.assertEqual(find_problems(self.root), [])

    def test_service_missing_app_connection_entirely_is_caught(self):
        # Exactly the real "finance" regression: a live interceptor, but AppConnection never
        # referenced anywhere in the service.
        self._write_interceptor("finance")

        problems = find_problems(self.root)
        self.assertEqual(len(problems), 1)
        self.assertIn("finance", problems[0])
        self.assertIn('GetConnectionString("AppConnection")', problems[0])

    def test_differently_named_interceptor_is_still_found(self):
        # The real "user-service" case: UserServiceTenantConnectionInterceptor.cs, not the
        # standard TenantDbConnectionInterceptor.cs name.
        self._write_interceptor("masterdata/user-service", filename="UserServiceTenantConnectionInterceptor.cs")

        problems = find_problems(self.root)
        self.assertEqual(len(problems), 1)
        self.assertIn("masterdata/user-service", problems[0])

    def test_nested_service_dir_with_app_connection_has_no_problems(self):
        self._write_interceptor("masterdata/user-service", filename="UserServiceTenantConnectionInterceptor.cs")
        self._write_app_connection_reference("masterdata/user-service")

        self.assertEqual(find_problems(self.root), [])

    def test_app_connection_reference_anywhere_under_service_root_counts(self):
        # Doesn't have to be in Program.cs specifically — e.g. crm/operations/procurement wire it
        # through a separate ServiceRegistration extension file instead.
        self._write_interceptor("crm")
        self._write_app_connection_reference("crm", rel_path="src/CrmService.Infrastructure/ServiceRegistration/InfrastructureServiceRegistration.cs")

        self.assertEqual(find_problems(self.root), [])

    def test_multiple_services_only_flags_the_one_missing_it(self):
        self._write_interceptor("finance")
        self._write_interceptor("crm")
        self._write_app_connection_reference("crm")

        problems = find_problems(self.root)
        self.assertEqual(len(problems), 1)
        self.assertIn("finance", problems[0])


if __name__ == "__main__":
    unittest.main()
