"""
Tests for validate_kubernetes_manifest_reachability.py — real fixture files under a temp
directory per scenario. Run with:
    python3 -m unittest test_validate_kubernetes_manifest_reachability -v
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from validate_kubernetes_manifest_reachability import check, ALLOWLIST

APPLICATION_MANIFEST = """
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: test-app
spec:
  source:
    path: {path}
"""

EXTERNAL_CHART_APPLICATION = """
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: external
spec:
  source:
    chart: velero
    repoURL: https://example.com/charts
"""


class KubernetesManifestReachabilityTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.kube = self.root / "kubernetes"
        (self.kube / "argocd").mkdir(parents=True)

    def tearDown(self):
        self.tmp.cleanup()

    def _write_application(self, name: str, path: str | None):
        text = EXTERNAL_CHART_APPLICATION if path is None else APPLICATION_MANIFEST.format(path=path)
        (self.kube / "argocd" / f"{name}.yaml").write_text(text)

    def test_manifest_inside_reachable_chart_is_fine(self):
        chart_dir = self.kube / "helm-charts" / "some-service"
        (chart_dir / "templates").mkdir(parents=True)
        (chart_dir / "Chart.yaml").write_text("apiVersion: v2\nname: some-service\n")
        (chart_dir / "templates" / "deployment.yaml").write_text("kind: Deployment\n")
        self._write_application("main", "kubernetes/helm-charts/some-service")

        self.assertEqual(check(self.root), [])

    def test_orphaned_manifest_is_flagged(self):
        chart_dir = self.kube / "helm-charts" / "some-service"
        (chart_dir / "templates").mkdir(parents=True)
        (chart_dir / "Chart.yaml").write_text("apiVersion: v2\nname: some-service\n")
        (chart_dir / "templates" / "deployment.yaml").write_text("kind: Deployment\n")
        self._write_application("main", "kubernetes/helm-charts/some-service")

        orphan_dir = self.kube / "backups"
        orphan_dir.mkdir()
        (orphan_dir / "cronjob-postgres-backup.yaml").write_text("kind: CronJob\n")

        problems = check(self.root)
        self.assertEqual(len(problems), 1)
        self.assertIn("kubernetes/backups/cronjob-postgres-backup.yaml", problems[0])
        self.assertIn("not reachable", problems[0])

    def test_allowlisted_file_is_not_flagged_even_though_unreachable(self):
        chart_dir = self.kube / "helm-charts" / "some-service"
        (chart_dir / "templates").mkdir(parents=True)
        (chart_dir / "Chart.yaml").write_text("apiVersion: v2\nname: some-service\n")
        (chart_dir / "templates" / "deployment.yaml").write_text("kind: Deployment\n")
        self._write_application("main", "kubernetes/helm-charts/some-service")

        # Not an Application manifest, so the structural exemption below doesn't apply — this
        # one is unreachable only because it's on the real ALLOWLIST by explicit path.
        allowlisted_rel = next(iter(ALLOWLIST))
        allowlisted_path = self.root / allowlisted_rel
        allowlisted_path.parent.mkdir(parents=True, exist_ok=True)
        allowlisted_path.write_text("kind: ClusterIssuer\n")

        problems = check(self.root)
        self.assertEqual(problems, [])

    def test_application_manifest_is_structurally_exempt_regardless_of_filename(self):
        # Any file declaring `kind: Application` is unreachable-by-definition — this must hold
        # for a brand new Application manifest too, not just the ones hardcoded in ALLOWLIST,
        # otherwise adding a fourth Application would need this checker updated to avoid a false
        # positive (the exact anti-pattern #198 exists to catch elsewhere).
        chart_dir = self.kube / "helm-charts" / "some-service"
        (chart_dir / "templates").mkdir(parents=True)
        (chart_dir / "Chart.yaml").write_text("apiVersion: v2\nname: some-service\n")
        (chart_dir / "templates" / "deployment.yaml").write_text("kind: Deployment\n")
        self._write_application("main", "kubernetes/helm-charts/some-service")
        self._write_application("brand-new-registrar", "kubernetes/helm-charts/some-service")

        self.assertNotIn("kubernetes/argocd/brand-new-registrar.yaml", ALLOWLIST)
        problems = check(self.root)
        self.assertEqual(problems, [])

    def test_local_chart_dependency_is_transitively_reachable(self):
        platform_dir = self.kube / "helm-charts" / "platform"
        platform_dir.mkdir(parents=True)
        platform_dir_yaml = platform_dir / "Chart.yaml"
        platform_dir_yaml.write_text(
            'apiVersion: v2\nname: platform\ndependencies:\n'
            '  - name: sub-service\n    repository: "file://../sub-service"\n'
        )

        sub_dir = self.kube / "helm-charts" / "sub-service"
        (sub_dir / "templates").mkdir(parents=True)
        (sub_dir / "Chart.yaml").write_text("apiVersion: v2\nname: sub-service\n")
        (sub_dir / "templates" / "deployment.yaml").write_text("kind: Deployment\n")

        self._write_application("main", "kubernetes/helm-charts/platform")

        self.assertEqual(check(self.root), [])

    def test_application_with_no_local_path_does_not_crash(self):
        chart_dir = self.kube / "helm-charts" / "some-service"
        (chart_dir / "templates").mkdir(parents=True)
        (chart_dir / "Chart.yaml").write_text("apiVersion: v2\nname: some-service\n")
        (chart_dir / "templates" / "deployment.yaml").write_text("kind: Deployment\n")
        self._write_application("main", "kubernetes/helm-charts/some-service")
        self._write_application("external", None)

        self.assertEqual(check(self.root), [])

    def test_no_manifests_at_all_is_a_checker_bug_not_a_clean_pass(self):
        empty_root = Path(self.tmp.name) / "empty"
        empty_root.mkdir()
        problems = check(empty_root)
        self.assertEqual(len(problems), 1)
        self.assertIn("checker needs updating", problems[0])


if __name__ == "__main__":
    unittest.main()
