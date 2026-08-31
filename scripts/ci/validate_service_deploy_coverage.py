#!/usr/bin/env python3
"""
Fail when a real Helm chart directory is missing from the deploy/repackage automation.

Three places have to agree on the set of deployable services, and nothing enforces it:

  1. kubernetes/helm-charts/<chart>/ — the actual chart directories.
  2. .github/workflows/repackage-helm-charts.yml — a `paths:` trigger list AND a bash
     CHART_DIRS associative array, both hand-maintained, both meant to cover every chart.
  3. .github/workflows/deploy-to-kubernetes.yml — two bash `case` statements (image-name
     lookup and YQ_PATH/HELM_CHART lookup) that every build-*.yml's `service-name:` input
     must have an arm for.

This is #227 item 3: "the mapping is invisible until a deploy silently targets nothing."
Found by hand while working that issue: repackage-helm-charts.yml was missing FIVE charts
entirely (crm, procurement, hr, reporting, subcontracts) — a direct edit to any of those
chart directories would never trigger a repackage, so the packaged .tgz ArgoCD reads from
would silently go stale relative to the chart source.

Deliberately regex-based, not a YAML/bash parser — matches the style of every other
validator in this directory, which read code as text rather than building a real AST.

Usage:  python3 scripts/ci/validate_service_deploy_coverage.py
Exit:   0 clean, 1 a chart is missing coverage somewhere, 2 could not parse (treated as
        failure — a checker that silently finds nothing is the bug this exists to prevent).
"""
import glob
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHARTS_DIR = os.path.join(REPO, "kubernetes/helm-charts")
REPACKAGE_WORKFLOW = os.path.join(REPO, ".github/workflows/repackage-helm-charts.yml")
DEPLOY_WORKFLOW = os.path.join(REPO, ".github/workflows/deploy-to-kubernetes.yml")
BUILD_WORKFLOWS = os.path.join(REPO, ".github/workflows/build-*.yml")

# The umbrella chart that bundles all the others — not itself a deployable service.
NOT_A_SERVICE_CHART = {"lante-erp-platform"}


def die(msg):
    print(f"::error::{msg}")
    sys.exit(2)


def real_chart_dirs():
    if not os.path.isdir(CHARTS_DIR):
        die(f"{CHARTS_DIR} does not exist — has the Helm chart layout moved?")
    dirs = {
        d for d in os.listdir(CHARTS_DIR)
        if os.path.isdir(os.path.join(CHARTS_DIR, d)) and d not in NOT_A_SERVICE_CHART
    }
    if not dirs:
        die(f"{CHARTS_DIR} has zero chart directories — parsed the wrong path?")
    return dirs


def repackage_paths(src):
    """`- kubernetes/helm-charts/<chart>/**` entries under the push-trigger `paths:` list."""
    found = set(re.findall(r"- kubernetes/helm-charts/([a-z0-9-]+)/\*\*", src))
    if not found:
        die("repackage-helm-charts.yml: parsed zero trigger paths — format changed?")
    return found


def repackage_chart_dirs_map(src):
    """Bash associative-array keys: `[chart-name]="chart-name"` inside CHART_DIRS=(...)."""
    if "CHART_DIRS" not in src:
        die("repackage-helm-charts.yml: no CHART_DIRS array — the checker's assumption is stale.")
    body = src.split("CHART_DIRS=(", 1)[1].split(")", 1)[0]
    found = set(re.findall(r'\[([a-z0-9-]+)\]="[a-z0-9-]+"', body))
    if not found:
        die("repackage-helm-charts.yml: CHART_DIRS parsed to zero entries — format changed?")
    return found


def deploy_case_targets(src, case_start_marker):
    """
    HELM_CHART/IMAGE_NAME values assigned inside the case statement that starts at
    `case_start_marker` (there are two case statements in this file; each is asked for by name).
    """
    if case_start_marker not in src:
        die(f"deploy-to-kubernetes.yml: no {case_start_marker!r} — the checker's assumption is stale.")
    body = src.split(case_start_marker, 1)[1].split("esac", 1)[0]
    found = set(re.findall(r'(?:IMAGE_NAME|HELM_CHART)="([a-z0-9-]+)"', body))
    if not found:
        die(f"deploy-to-kubernetes.yml: zero targets parsed after {case_start_marker!r} — format changed?")
    return found


def deploy_case_arms(src, case_start_marker):
    """The service-name labels (case arms) themselves, e.g. `operations)` -> `operations`."""
    if case_start_marker not in src:
        die(f"deploy-to-kubernetes.yml: no {case_start_marker!r} — the checker's assumption is stale.")
    body = src.split(case_start_marker, 1)[1].split("esac", 1)[0]
    found = set(re.findall(r"^\s*([a-z0-9-]+)\)", body, re.MULTILINE)) - {"*"}
    if not found:
        die(f"deploy-to-kubernetes.yml: zero case arms parsed after {case_start_marker!r} — format changed?")
    return found


def build_workflow_service_names():
    """The `service-name:` input every build-*.yml passes to deploy-to-kubernetes.yml."""
    names = {}
    for path in sorted(glob.glob(BUILD_WORKFLOWS)):
        with open(path, encoding="utf-8") as fh:
            src = fh.read()
        m = re.search(r"service-name:\s*([a-zA-Z0-9-]+)", src)
        if m:
            names[m.group(1)] = os.path.basename(path)
    if not names:
        die(f"{BUILD_WORKFLOWS}: parsed zero service-name inputs — format changed?")
    return names


# service-name (as used by build-*.yml / deploy-to-kubernetes.yml) -> chart directory name.
# The two disagree for exactly the three services #227 item 3 is about; every other service
# name already equals its chart directory name.
SERVICE_NAME_TO_CHART = {
    "operations": "operation-service",
    "stores": "store-service",
    "license": "license-service",
}


def main():
    charts = real_chart_dirs()

    with open(REPACKAGE_WORKFLOW, encoding="utf-8") as fh:
        repackage_src = fh.read()
    paths = repackage_paths(repackage_src)
    chart_dirs_map = repackage_chart_dirs_map(repackage_src)

    with open(DEPLOY_WORKFLOW, encoding="utf-8") as fh:
        deploy_src = fh.read()
    image_targets = deploy_case_targets(deploy_src, "Verify image exists in GHCR")
    helm_targets = deploy_case_targets(deploy_src, "Update image tag and repackage chart")
    deploy_arms = deploy_case_arms(deploy_src, "Verify image exists in GHCR")

    build_names = build_workflow_service_names()

    errors = []

    missing_from_paths = charts - paths
    if missing_from_paths:
        errors.append(
            f"Chart(s) missing from repackage-helm-charts.yml's `paths:` trigger: "
            f"{', '.join(sorted(missing_from_paths))}. A direct edit to that chart's source "
            f"will never trigger a repackage."
        )
    stale_paths = paths - charts
    if stale_paths:
        errors.append(
            f"repackage-helm-charts.yml's `paths:` trigger references chart(s) that no longer "
            f"exist: {', '.join(sorted(stale_paths))}."
        )

    missing_from_map = charts - chart_dirs_map
    if missing_from_map:
        errors.append(
            f"Chart(s) missing from repackage-helm-charts.yml's CHART_DIRS map: "
            f"{', '.join(sorted(missing_from_map))}."
        )
    stale_map = chart_dirs_map - charts
    if stale_map:
        errors.append(
            f"repackage-helm-charts.yml's CHART_DIRS map references chart(s) that no longer "
            f"exist: {', '.join(sorted(stale_map))}."
        )

    missing_from_images = charts - image_targets
    if missing_from_images:
        errors.append(
            f"Chart(s) with no matching IMAGE_NAME in deploy-to-kubernetes.yml's "
            f"'Verify image exists in GHCR' case: {', '.join(sorted(missing_from_images))}."
        )

    missing_from_helm = charts - helm_targets
    if missing_from_helm:
        errors.append(
            f"Chart(s) with no matching HELM_CHART in deploy-to-kubernetes.yml's "
            f"'Update image tag and repackage chart' case: {', '.join(sorted(missing_from_helm))}."
        )

    for service_name, workflow_file in sorted(build_names.items()):
        if service_name not in deploy_arms:
            errors.append(
                f"{workflow_file} passes service-name '{service_name}', which has no case arm "
                f"in deploy-to-kubernetes.yml — its deploy step will hit the unknown-service error "
                f"(or, before that error existed, silently deploy nothing)."
            )

    print(f"chart directories: {len(charts)} | repackage paths: {len(paths)} | "
          f"CHART_DIRS entries: {len(chart_dirs_map)} | build workflows: {len(build_names)}")

    if errors:
        for e in errors:
            print(f"::error::{e}")
        print(f"\n{len(errors)} coverage gap(s) found. See #227.")
        return 1

    print("\nEvery chart directory is covered by both workflows.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
