"""
Tests for validate_service_infrastructure.py — real fixture files under a temp directory per
scenario. Run with: python3 -m unittest scripts/ci/test_validate_service_infrastructure.py
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from validate_service_infrastructure import find_wiring_problems

GOOD_BUILD_WORKFLOW = """
jobs:
  build:
    steps:
      - uses: docker/build-push-action@v5
        with:
          context: ./packages/microservices/{dir}
  deploy:
    uses: ./.github/workflows/deploy-to-kubernetes.yml
    with:
      service-name: {key}
"""

GOOD_DEPLOY_CASE = """
          case "$SERVICE" in
            {key})
              YQ_PATH=".x.image.tag"
              HELM_CHART="{chart}"
              ;;
          esac
"""


class ServiceInfrastructureTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.workflows_dir = self.root / "workflows"
        self.workflows_dir.mkdir()
        self.helm_charts_dir = self.root / "charts"
        self.helm_charts_dir.mkdir()
        self.deploy_workflow = self.root / "deploy.yml"
        self.platform_chart_yaml = self.root / "Chart.yaml"

    def tearDown(self):
        self.tmp.cleanup()

    def _setup_fully_wired_service(self, dir_name: str, key: str, chart: str):
        (self.root / "packages/microservices" / dir_name).mkdir(parents=True, exist_ok=True)
        (self.root / "packages/microservices" / dir_name / "Dockerfile").touch()
        (self.workflows_dir / f"build-{dir_name}.yml").write_text(
            GOOD_BUILD_WORKFLOW.format(dir=dir_name, key=key)
        )
        self.deploy_workflow.write_text(GOOD_DEPLOY_CASE.format(key=key, chart=chart))
        (self.helm_charts_dir / chart).mkdir(parents=True, exist_ok=True)
        self.platform_chart_yaml.write_text(f"dependencies:\n  - name: {chart}\n")

    def _run(self):
        return find_wiring_problems(
            repo_root=self.root,
            workflows_dir=self.workflows_dir,
            deploy_workflow=self.deploy_workflow,
            helm_charts_dir=self.helm_charts_dir,
            platform_chart_yaml=self.platform_chart_yaml,
        )

    def test_fully_wired_service_has_no_problems(self):
        self._setup_fully_wired_service("widgets", "widgets", "widgets-service")
        self.assertEqual(self._run(), [])

    def test_build_workflow_with_no_deploy_job_is_caught(self):
        # Builds and pushes an image, but never calls deploy-to-kubernetes.yml at all — no
        # 'service-name:' input anywhere in the workflow.
        self.deploy_workflow.write_text("")
        self.platform_chart_yaml.write_text("dependencies: []\n")
        (self.root / "packages/microservices/widgets").mkdir(parents=True)
        (self.root / "packages/microservices/widgets/Dockerfile").touch()
        (self.workflows_dir / "build-widgets.yml").write_text(
            "jobs:\n  build:\n    steps:\n      - uses: docker/build-push-action@v5\n"
            "        with:\n          context: ./packages/microservices/widgets\n"
        )

        problems = self._run()
        self.assertTrue(any(
            "widgets" in p and "no deploy job calling deploy-to-kubernetes.yml" in p
            for p in problems
        ))

    def test_dockerfile_with_no_build_workflow_is_caught(self):
        self.deploy_workflow.write_text("")
        self.platform_chart_yaml.write_text("dependencies: []\n")
        (self.root / "packages/microservices/orphan").mkdir(parents=True)
        (self.root / "packages/microservices/orphan/Dockerfile").touch()

        problems = self._run()
        self.assertTrue(any("orphan" in p and "no build-*.yml workflow" in p for p in problems))

    def test_service_name_missing_from_deploy_case_statement_is_caught(self):
        self._setup_fully_wired_service("widgets", "widgets", "widgets-service")
        # Deploy workflow has no matching case for "widgets" at all.
        self.deploy_workflow.write_text("case \"$SERVICE\" in\nesac\n")

        problems = self._run()
        self.assertTrue(any("has no matching case" in p for p in problems))

    def test_helm_chart_directory_missing_is_caught(self):
        self._setup_fully_wired_service("widgets", "widgets", "widgets-service")
        # Chart directory doesn't actually exist even though the case statement claims it does.
        import shutil
        shutil.rmtree(self.helm_charts_dir / "widgets-service")

        problems = self._run()
        self.assertTrue(any("doesn't exist" in p for p in problems))

    def test_helm_chart_missing_from_platform_dependencies_is_caught(self):
        self._setup_fully_wired_service("widgets", "widgets", "widgets-service")
        self.platform_chart_yaml.write_text("dependencies:\n  - name: some-other-chart\n")

        problems = self._run()
        self.assertTrue(any("is not listed as a dependency" in p for p in problems))


if __name__ == "__main__":
    unittest.main()
