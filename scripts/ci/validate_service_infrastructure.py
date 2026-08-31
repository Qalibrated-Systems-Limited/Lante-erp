#!/usr/bin/env python3
"""
Guards against the two documented, previously-real CI/CD failure modes in project history: "a new
service ships backend code + a Dockerfile but nobody added a build-*.yml workflow for it, so its
image is never built" (silently invisible until someone notices pods stuck in InvalidImageName),
and "a new service has a build workflow but its Helm chart was never wired into
deploy-to-kubernetes.yml's case statement / the platform chart's dependency list" (image builds
fine, but the running cluster never picks it up).

Deliberately generic, not hardcoded to any specific service list — discovers every real Dockerfile
in the repo and every actual build-*.yml/deploy-to-kubernetes.yml wiring each run, so it keeps
working unmodified as services are added. Skips vendored/build-artifact directories that can
contain incidental Dockerfiles (node_modules, bin, obj, .git).
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS_DIR = REPO_ROOT / ".github/workflows"
DEPLOY_WORKFLOW = WORKFLOWS_DIR / "deploy-to-kubernetes.yml"
HELM_CHARTS_DIR = REPO_ROOT / "kubernetes/helm-charts"
PLATFORM_CHART_YAML = HELM_CHARTS_DIR / "lante-erp-platform/Chart.yaml"

SKIP_DIR_NAMES = {"node_modules", "bin", "obj", ".git"}


def find_dockerfile_dirs(repo_root: Path = REPO_ROOT) -> set[Path]:
    """Every directory containing a Dockerfile, as a path relative to repo_root, excluding
    vendored/build-artifact trees that could contain an incidental Dockerfile."""
    dirs = set()
    for dockerfile in repo_root.glob("**/Dockerfile"):
        rel_parts = dockerfile.relative_to(repo_root).parts
        if any(part in SKIP_DIR_NAMES or part.startswith(".") for part in rel_parts):
            continue
        dirs.add(dockerfile.parent.relative_to(repo_root))
    return dirs


def find_build_workflow_contexts(workflows_dir: Path = WORKFLOWS_DIR) -> dict[Path, Path]:
    """{docker build context dir (relative to repo root): workflow file} for every build-*.yml."""
    contexts = {}
    for wf in sorted(workflows_dir.glob("build-*.yml")):
        text = wf.read_text()
        for m in re.finditer(r"context:\s*\./(\S+)", text):
            contexts[Path(m.group(1))] = wf
    return contexts


def find_service_names_by_workflow(workflows_dir: Path = WORKFLOWS_DIR) -> dict[Path, str]:
    """{workflow file: service-name value passed to the reusable deploy workflow}."""
    result = {}
    for wf in sorted(workflows_dir.glob("build-*.yml")):
        text = wf.read_text()
        m = re.search(r"service-name:\s*(\S+)", text)
        if m:
            result[wf] = m.group(1)
    return result


def find_helm_chart_by_service_name(deploy_workflow: Path = DEPLOY_WORKFLOW) -> dict[str, str]:
    """{service-name key used in build-*.yml: HELM_CHART value from deploy-to-kubernetes.yml's
    case statement}, parsed from the case block that explicitly assigns HELM_CHART="..."""
    text = deploy_workflow.read_text()
    return dict(re.findall(r'(\S+)\)\s*\n\s*YQ_PATH=.*\n\s*HELM_CHART="([^"]+)"', text))


def find_wiring_problems(
    repo_root: Path = REPO_ROOT,
    workflows_dir: Path = WORKFLOWS_DIR,
    deploy_workflow: Path = DEPLOY_WORKFLOW,
    helm_charts_dir: Path = HELM_CHARTS_DIR,
    platform_chart_yaml: Path = PLATFORM_CHART_YAML,
) -> list[str]:
    problems = []

    dockerfile_dirs = find_dockerfile_dirs(repo_root)
    build_contexts = find_build_workflow_contexts(workflows_dir)
    service_names = find_service_names_by_workflow(workflows_dir)
    for d in sorted(dockerfile_dirs):
        if d not in build_contexts:
            problems.append(
                f"'{d}' has a Dockerfile but no build-*.yml workflow uses it as a docker build "
                f"context — its image is never built, and it can never be deployed."
            )
        elif build_contexts[d] not in service_names:
            problems.append(
                f"'{d}' has a build-*.yml workflow ({build_contexts[d].name}) that builds and "
                f"pushes its image, but that workflow has no deploy job calling "
                f"deploy-to-kubernetes.yml (no 'service-name:' input found) — the image builds, "
                f"but is never actually deployed to the cluster."
            )

    helm_by_service = find_helm_chart_by_service_name(deploy_workflow)
    chart_yaml_text = platform_chart_yaml.read_text() if platform_chart_yaml.exists() else ""

    for wf, service_name in service_names.items():
        if service_name not in helm_by_service:
            problems.append(
                f"{wf.name}: service-name '{service_name}' has no matching case in "
                f"{deploy_workflow.relative_to(repo_root)} — the image builds, but the deploy "
                f"step's image-tag update and Helm repackage silently do nothing for it."
            )
            continue

        helm_chart = helm_by_service[service_name]
        if not (helm_charts_dir / helm_chart).is_dir():
            problems.append(
                f"{wf.name}: service-name '{service_name}' maps to Helm chart '{helm_chart}', "
                f"but {helm_charts_dir.relative_to(repo_root)}/{helm_chart} doesn't exist."
            )
        if f"name: {helm_chart}" not in chart_yaml_text:
            problems.append(
                f"{wf.name}: Helm chart '{helm_chart}' is not listed as a dependency in "
                f"{platform_chart_yaml.relative_to(repo_root)} — ArgoCD will never deploy it "
                f"as part of the platform release."
            )

    return problems


def main() -> int:
    problems = find_wiring_problems()
    if problems:
        print(f"::error::Found {len(problems)} service deployment wiring problem(s):")
        for p in problems:
            print(f"  - {p}")
        return 1

    print(f"OK — {len(find_dockerfile_dirs())} Dockerfile(s), all with a build workflow and Helm chart wiring.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
