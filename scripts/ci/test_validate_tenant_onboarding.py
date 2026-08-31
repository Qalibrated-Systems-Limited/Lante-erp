"""
Tests for validate_tenant_onboarding.py — builds real fixture files under a temp directory for
each scenario rather than mocking, so a change to the parsing regexes or file layout assumptions
would actually break these tests too. Run with: python3 -m unittest scripts/ci/test_validate_tenant_onboarding.py
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from validate_tenant_onboarding import find_wiring_problems

PLATFORM_SERVICES_TEMPLATE = """
public static class PlatformServices
{{
    public const string User = "user";
    public const string Ticketing = "ticketing";
    public const string Reporting = "reporting";

    public static readonly IReadOnlyList<string> Business =
        new[] {{ {business_idents} }};
}}
"""


class TenantOnboardingWiringTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

        self.platform_services_cs = self.root / "PlatformServices.cs"
        self.microservices_root = self.root / "microservices"
        self.helm_deployment = self.root / "deployment.yaml"
        self.dev_appsettings = self.root / "appsettings.Development.json"
        self.docker_compose = self.root / "docker-compose.yml"
        self.microservices_root.mkdir()

    def tearDown(self):
        self.tmp.cleanup()

    def _write_platform_services(self, business_idents: str):
        self.platform_services_cs.write_text(PLATFORM_SERVICES_TEMPLATE.format(business_idents=business_idents))

    def _write_fully_wired_config(self, keys: list[str]):
        self.helm_deployment.write_text("\n".join(f'- name: ProvisioningTargets__{k}' for k in keys))
        self.dev_appsettings.write_text("{" + ", ".join(f'"{k}": "http://localhost"' for k in keys) + "}")
        self.docker_compose.write_text("\n".join(f'ProvisioningTargets__{k}=http://x' for k in keys))

    def _add_controller(self, service_dir: str):
        controller_dir = self.microservices_root / service_dir / "src/SomeApi/Controllers"
        controller_dir.mkdir(parents=True, exist_ok=True)
        (controller_dir / "InternalProvisioningController.cs").touch()

    def _add_tenant_migrations(self, service_dir: str):
        migrations_dir = self.microservices_root / service_dir / "src/SomeInfra/Migrations/Tenant"
        migrations_dir.mkdir(parents=True, exist_ok=True)
        (migrations_dir / "20260101000000_TenantInitialCreate.cs").touch()

    def _add_interceptor(self, service_dir: str):
        data_dir = self.microservices_root / service_dir / "src/SomeInfra/Data"
        data_dir.mkdir(parents=True, exist_ok=True)
        (data_dir / "TenantDbConnectionInterceptor.cs").touch()

    def _run(self):
        return find_wiring_problems(
            platform_services_cs=self.platform_services_cs,
            microservices_root=self.microservices_root,
            helm_deployment=self.helm_deployment,
            dev_appsettings=self.dev_appsettings,
            docker_compose=self.docker_compose,
            service_key_by_dir={"ticketing": "ticketing", "reporting": "reporting"},
        )

    def test_fully_consistent_state_has_no_problems(self):
        self._write_platform_services("Ticketing, Reporting")
        self._write_fully_wired_config(["ticketing", "reporting"])
        self._add_controller("ticketing")
        self._add_controller("reporting")

        self.assertEqual(self._run(), [])

    def test_service_with_controller_missing_from_business_is_caught(self):
        # Exactly today's real "reporting" regression: a working controller exists, but the
        # service was never added to PlatformServices.Business.
        self._write_platform_services("Ticketing")
        self._write_fully_wired_config(["ticketing"])
        self._add_controller("ticketing")
        self._add_controller("reporting")

        problems = self._run()
        self.assertEqual(len(problems), 1)
        self.assertIn("reporting", problems[0])
        self.assertIn("PlatformServices.Business doesn't include it", problems[0])

    def test_business_service_missing_helm_config_is_caught(self):
        self._write_platform_services("Ticketing, Reporting")
        self._write_fully_wired_config(["ticketing", "reporting"])
        # Simulate "stores"-style gap: present everywhere except one config file.
        self.helm_deployment.write_text("- name: ProvisioningTargets__ticketing")
        self._add_controller("ticketing")
        self._add_controller("reporting")

        problems = self._run()
        self.assertTrue(any("reporting" in p and self.helm_deployment.name in p for p in problems))

    def test_business_service_missing_dev_appsettings_is_caught(self):
        self._write_platform_services("Ticketing, Reporting")
        self._write_fully_wired_config(["ticketing", "reporting"])
        self.dev_appsettings.write_text('{"ticketing": "http://localhost"}')
        self._add_controller("ticketing")
        self._add_controller("reporting")

        problems = self._run()
        self.assertTrue(any("reporting" in p and self.dev_appsettings.name in p for p in problems))

    def test_business_service_missing_docker_compose_is_caught(self):
        self._write_platform_services("Ticketing, Reporting")
        self._write_fully_wired_config(["ticketing", "reporting"])
        self.docker_compose.write_text("ProvisioningTargets__ticketing=http://x")
        self._add_controller("ticketing")
        self._add_controller("reporting")

        problems = self._run()
        self.assertTrue(any("reporting" in p and self.docker_compose.name in p for p in problems))

    def test_tenant_migrations_with_no_controller_at_all_is_caught(self):
        # The gap the controller-based check alone can't see: a brand-new service with real
        # tenant-schema migrations but zero provisioning code yet — nothing to cross-reference
        # against PlatformServices.Business, so it would otherwise look like "nothing to check"
        # rather than "something is missing".
        self._write_platform_services("Ticketing")
        self._write_fully_wired_config(["ticketing"])
        self._add_controller("ticketing")
        self._add_tenant_migrations("reporting")  # no _add_controller("reporting") at all

        problems = self._run()
        self.assertTrue(any(
            "reporting" in p and "no InternalProvisioningController.cs at all" in p
            for p in problems
        ))

    def test_tenant_migrations_with_controller_present_has_no_extra_problem(self):
        self._write_platform_services("Ticketing, Reporting")
        self._write_fully_wired_config(["ticketing", "reporting"])
        self._add_controller("ticketing")
        self._add_controller("reporting")
        self._add_tenant_migrations("reporting")

        self.assertEqual(self._run(), [])

    def test_interceptor_with_no_controller_and_no_tenant_migrations_is_caught(self):
        # Exactly today's real "finance" regression: a live TenantDbConnectionInterceptor.cs
        # routes connections to a per-request tenant schema, but there's no
        # InternalProvisioningController.cs and no Migrations/Tenant folder at all — neither of
        # the other two checks has anything to cross-reference, so this is the only signal that
        # catches it.
        self._write_platform_services("Ticketing")
        self._write_fully_wired_config(["ticketing"])
        self._add_controller("ticketing")
        self._add_interceptor("reporting")  # no controller, no tenant migrations for reporting

        problems = self._run()
        self.assertTrue(any(
            "reporting" in p and "no InternalProvisioningController.cs and no Migrations/Tenant" in p
            for p in problems
        ))

    def test_interceptor_with_controller_present_has_no_extra_problem(self):
        self._write_platform_services("Ticketing, Reporting")
        self._write_fully_wired_config(["ticketing", "reporting"])
        self._add_controller("ticketing")
        self._add_controller("reporting")
        self._add_interceptor("reporting")

        self.assertEqual(self._run(), [])

    def test_interceptor_with_only_tenant_migrations_has_no_extra_problem(self):
        # Covered by the separate "tenant migrations with no controller" problem already;
        # shouldn't be double-reported by the interceptor check too.
        self._write_platform_services("Ticketing")
        self._write_fully_wired_config(["ticketing"])
        self._add_controller("ticketing")
        self._add_tenant_migrations("reporting")
        self._add_interceptor("reporting")

        problems = self._run()
        self.assertEqual(len(problems), 1)
        self.assertIn("no InternalProvisioningController.cs at all", problems[0])

    def test_user_service_controller_is_exempt_from_business_list(self):
        # "user" is always provisioned in-process, deliberately never in PlatformServices.Business.
        self._write_platform_services("Ticketing")
        self._write_fully_wired_config(["ticketing"])
        self._add_controller("ticketing")
        self._add_controller("masterdata/user-service")

        problems = find_wiring_problems(
            platform_services_cs=self.platform_services_cs,
            microservices_root=self.microservices_root,
            helm_deployment=self.helm_deployment,
            dev_appsettings=self.dev_appsettings,
            docker_compose=self.docker_compose,
            service_key_by_dir={"ticketing": "ticketing", "masterdata/user-service": "user"},
        )
        self.assertEqual(problems, [])


if __name__ == "__main__":
    unittest.main()
