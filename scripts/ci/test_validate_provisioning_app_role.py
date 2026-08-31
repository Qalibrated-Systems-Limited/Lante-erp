"""
Tests for validate_provisioning_app_role.py — builds real fixture files under a temp
directory for each scenario rather than mocking. Run with:
python3 -m unittest test_validate_provisioning_app_role -v
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from validate_provisioning_app_role import canonical_app_role, check, find_fallbacks

SECRETS_SH = """#!/usr/bin/env bash
DB_USER="lante_user"
APP_DB_USER="qalicore_app"
APP_DB_PASSWORD=$(openssl rand -base64 24)
"""

SERVICE_CS = """namespace Whatever;
public partial class TenantProvisioningService
{{
    private async Task<string?> GrantAppRoleAsync(DbContext ctx, string schema, CancellationToken ct)
    {{
        var appRole = _configuration["ProvisioningAppRole"] ?? "{role}";
        return null;
    }}
}}
"""

# The #196 fallback under a spelling/file the original glob and regex both missed — see #198's
# widening of PROVISIONING_GLOB (TenantProvisioningService.cs only -> every .cs file) and
# FALLBACK_RE (required `_configuration[...]` -> any `[...]` indexer).
PROGRAM_CS = """var appRole = builder.Configuration["ProvisioningAppRole"] ?? "{role}";
"""


def build(root: Path, *, secrets: str = SECRETS_SH, services: dict[str, str] | None = None) -> None:
    (root / "kubernetes/secrets").mkdir(parents=True, exist_ok=True)
    (root / "kubernetes/secrets/generate-secrets.sh").write_text(secrets, encoding="utf-8")
    for name, role in (services or {}).items():
        d = root / f"packages/microservices/{name}/src/{name}.Infrastructure/Services"
        d.mkdir(parents=True, exist_ok=True)
        (d / "TenantProvisioningService.cs").write_text(SERVICE_CS.format(role=role), encoding="utf-8")


def build_program_cs(root: Path, service: str, role: str) -> None:
    d = root / f"packages/microservices/{service}/src/{service}.Api"
    d.mkdir(parents=True, exist_ok=True)
    (d / "Program.cs").write_text(PROGRAM_CS.format(role=role), encoding="utf-8")


class TestCanonicalRole(unittest.TestCase):
    def test_reads_app_db_user(self):
        self.assertEqual(canonical_app_role(SECRETS_SH), "qalicore_app")

    def test_reads_unquoted(self):
        self.assertEqual(canonical_app_role("APP_DB_USER=some_role\n"), "some_role")

    def test_returns_none_when_absent(self):
        self.assertIsNone(canonical_app_role("DB_USER=x\n"))


class TestFindFallbacks(unittest.TestCase):
    def test_finds_each_service(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            build(root, services={"alpha": "qalicore_app", "beta": "lante_app"})
            found = find_fallbacks(root)
            self.assertEqual(len(found), 2)
            self.assertEqual(sorted(r for rs in found.values() for r in rs),
                             ["lante_app", "qalicore_app"])

    def test_finds_a_differently_spelled_fallback_in_a_differently_named_file(self):
        """The exact #196-in-#198 blind spot: Program.cs, not TenantProvisioningService.cs, and
        `builder.Configuration[...]`, not `_configuration[...]`."""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            build(root, services={})
            build_program_cs(root, "userservice", "lante_app")
            found = find_fallbacks(root)
            self.assertEqual(len(found), 1)
            self.assertEqual(list(found.values())[0], ["lante_app"])

    def test_ignores_build_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            build(root, services={"alpha": "qalicore_app"})
            obj = root / "packages/microservices/alpha/src/alpha.Infrastructure/obj/Debug"
            obj.mkdir(parents=True)
            (obj / "TenantProvisioningService.cs").write_text(
                SERVICE_CS.format(role="lante_app"), encoding="utf-8")
            self.assertEqual(len(find_fallbacks(root)), 1)


class TestCheck(unittest.TestCase):
    def test_passes_when_all_match(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            build(root, services={"alpha": "qalicore_app", "beta": "qalicore_app"})
            self.assertEqual(check(root), [])

    def test_flags_a_drifted_fallback_in_program_cs(self):
        """Regression for the live bug found by widening this check for #198:
        UserService.Api/Program.cs fell back to 'lante_app', unfixed, while the
        TenantProvisioningService.cs-only glob and _configuration[...]-only regex looked past it."""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            build(root, services={"alpha": "qalicore_app"})
            build_program_cs(root, "userservice", "lante_app")
            problems = check(root)
            self.assertEqual(len(problems), 1)
            self.assertIn("Program.cs", problems[0])
            self.assertIn("lante_app", problems[0])

    def test_flags_a_drifted_fallback(self):
        """The actual #196 shape: some services renamed, the Postgres role never was."""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            build(root, services={"alpha": "qalicore_app", "beta": "lante_app"})
            problems = check(root)
            self.assertEqual(len(problems), 1)
            self.assertIn("beta", problems[0])
            self.assertIn("lante_app", problems[0])
            self.assertIn("qalicore_app", problems[0])

    def test_flags_every_drifted_service(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            build(root, services={f"s{i}": "lante_app" for i in range(7)})
            self.assertEqual(len(check(root)), 7)

    def test_follows_a_deliberate_rename_of_the_source_of_truth(self):
        """Renaming APP_DB_USER and all fallbacks together must pass."""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            build(root,
                  secrets='APP_DB_USER="lante_app"\n',
                  services={"alpha": "lante_app", "beta": "lante_app"})
            self.assertEqual(check(root), [])

    def test_fails_loudly_when_secrets_file_is_missing(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "packages").mkdir()
            problems = check(root)
            self.assertEqual(len(problems), 1)
            self.assertIn("not found", problems[0])

    def test_fails_loudly_when_no_fallbacks_exist(self):
        """A refactor that removes the pattern must break this checker, not silently pass."""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            build(root, services={})
            problems = check(root)
            self.assertEqual(len(problems), 1)
            self.assertIn("needs", problems[0])


if __name__ == "__main__":
    unittest.main()
