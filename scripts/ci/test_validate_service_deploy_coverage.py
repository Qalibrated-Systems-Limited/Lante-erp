"""
Tests for validate_service_deploy_coverage.py. Builds real fixture files under a temp directory
so the parsers are exercised against actual file contents, and drives main() end to end against a
monkeypatched repo layout for the two scenarios that matter: full coverage, and the real #227 bug
(five charts missing from repackage-helm-charts.yml).

Run with: python3 -m unittest test_validate_service_deploy_coverage -v
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
from unittest import mock

import validate_service_deploy_coverage as mod

REPACKAGE_YML = """
on:
  push:
    branches: [main]
    paths:
      - kubernetes/helm-charts/gateway-service/**
      - kubernetes/helm-charts/user-service/**
jobs:
  repackage:
    steps:
      - name: Detect changed charts and repackage
        run: |
          declare -A CHART_DIRS
          CHART_DIRS=(
            [gateway-service]="gateway-service"
            [user-service]="user-service"
          )
"""

REPACKAGE_YML_FULL = REPACKAGE_YML.replace(
    "- kubernetes/helm-charts/user-service/**",
    "- kubernetes/helm-charts/user-service/**\n      - kubernetes/helm-charts/crm-service/**",
).replace(
    '[user-service]="user-service"',
    '[user-service]="user-service"\n            [crm-service]="crm-service"',
)

DEPLOY_YML = """
      - name: Verify image exists in GHCR
        run: |
          case "$SERVICE" in
            gateway)   IMAGE_NAME="gateway-service"   ;;
            user-service) IMAGE_NAME="user-service"    ;;
          esac

      - name: Update image tag and repackage chart
        run: |
          case "$SERVICE" in
            gateway)
              HELM_CHART="gateway-service"
              ;;
            user-service)
              HELM_CHART="user-service"
              ;;
          esac
"""

DEPLOY_YML_FULL = DEPLOY_YML.replace(
    'user-service) IMAGE_NAME="user-service"    ;;',
    'user-service) IMAGE_NAME="user-service"    ;;\n            crm)       IMAGE_NAME="crm-service"      ;;',
).replace(
    "            user-service)\n              HELM_CHART=\"user-service\"\n              ;;",
    "            user-service)\n              HELM_CHART=\"user-service\"\n              ;;\n            crm)\n              HELM_CHART=\"crm-service\"\n              ;;",
)

BUILD_GATEWAY_YML = "jobs:\n  build:\n    uses: ./.github/workflows/build.yml\n    with:\n      service-name: gateway\n"
BUILD_USER_YML = "jobs:\n  build:\n    uses: ./.github/workflows/build.yml\n    with:\n      service-name: user-service\n"
BUILD_CRM_YML = "jobs:\n  build:\n    uses: ./.github/workflows/build.yml\n    with:\n      service-name: crm\n"


class ParserTests(unittest.TestCase):
    def test_parses_repackage_trigger_paths(self):
        self.assertEqual(mod.repackage_paths(REPACKAGE_YML), {"gateway-service", "user-service"})

    def test_parses_chart_dirs_map(self):
        self.assertEqual(mod.repackage_chart_dirs_map(REPACKAGE_YML), {"gateway-service", "user-service"})

    def test_an_empty_trigger_list_is_a_hard_failure(self):
        with self.assertRaises(SystemExit) as ctx:
            mod.repackage_paths("paths:\n  - nothing/relevant/**")
        self.assertEqual(ctx.exception.code, 2)

    def test_parses_image_name_case_targets(self):
        targets = mod.deploy_case_targets(DEPLOY_YML, "Verify image exists in GHCR")
        self.assertEqual(targets, {"gateway-service", "user-service"})

    def test_parses_helm_chart_case_targets(self):
        targets = mod.deploy_case_targets(DEPLOY_YML, "Update image tag and repackage chart")
        self.assertEqual(targets, {"gateway-service", "user-service"})

    def test_parses_case_arms(self):
        arms = mod.deploy_case_arms(DEPLOY_YML, "Verify image exists in GHCR")
        self.assertEqual(arms, {"gateway", "user-service"})

    def test_a_missing_marker_is_a_hard_failure(self):
        with self.assertRaises(SystemExit) as ctx:
            mod.deploy_case_targets(DEPLOY_YML, "Some Step That Does Not Exist")
        self.assertEqual(ctx.exception.code, 2)


class MainEndToEndTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        (self.root / "kubernetes/helm-charts/lante-erp-platform").mkdir(parents=True)
        (self.root / "kubernetes/helm-charts/gateway-service").mkdir(parents=True)
        (self.root / "kubernetes/helm-charts/user-service").mkdir(parents=True)
        (self.root / "kubernetes/helm-charts/crm-service").mkdir(parents=True)
        (self.root / ".github/workflows").mkdir(parents=True)
        (self.root / ".github/workflows/build-gateway.yml").write_text(BUILD_GATEWAY_YML)
        (self.root / ".github/workflows/build-user.yml").write_text(BUILD_USER_YML)
        (self.root / ".github/workflows/build-crm.yml").write_text(BUILD_CRM_YML)

    def tearDown(self):
        self.tmp.cleanup()

    def _patch(self):
        return mock.patch.multiple(
            mod,
            CHARTS_DIR=str(self.root / "kubernetes/helm-charts"),
            REPACKAGE_WORKFLOW=str(self.root / ".github/workflows/repackage.yml"),
            DEPLOY_WORKFLOW=str(self.root / ".github/workflows/deploy.yml"),
            BUILD_WORKFLOWS=str(self.root / ".github/workflows/build-*.yml"),
        )

    def test_full_coverage_is_clean(self):
        (self.root / ".github/workflows/repackage.yml").write_text(REPACKAGE_YML_FULL)
        (self.root / ".github/workflows/deploy.yml").write_text(DEPLOY_YML_FULL)
        with self._patch():
            self.assertEqual(mod.main(), 0)

    def test_a_chart_missing_from_repackage_coverage_is_caught(self):
        # crm-service exists as a real chart dir and is covered by deploy.yml, but NOT by
        # repackage.yml — exactly the real #227 bug (5 charts missing from repackage coverage).
        (self.root / ".github/workflows/repackage.yml").write_text(REPACKAGE_YML)
        (self.root / ".github/workflows/deploy.yml").write_text(DEPLOY_YML_FULL)
        with self._patch():
            self.assertEqual(mod.main(), 1)

    def test_a_build_workflow_with_no_deploy_case_arm_is_caught(self):
        # crm-service is covered everywhere EXCEPT deploy.yml never gained a case arm for it —
        # the scenario a newly-added service hits if deploy-to-kubernetes.yml isn't updated.
        (self.root / ".github/workflows/repackage.yml").write_text(REPACKAGE_YML_FULL)
        (self.root / ".github/workflows/deploy.yml").write_text(DEPLOY_YML)
        with self._patch():
            self.assertEqual(mod.main(), 1)


if __name__ == "__main__":
    unittest.main()
