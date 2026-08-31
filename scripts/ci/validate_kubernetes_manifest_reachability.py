#!/usr/bin/env python3
"""
Guards against a Kubernetes manifest under kubernetes/ being committed, correct, and never
applied because it sits outside every path ArgoCD actually watches (issue #198).

The bug this generalizes (2026, issues #190/#192): kubernetes/backups/cronjob-postgres-backup.yaml
was a valid CronJob manifest, reviewed and merged, sitting in a directory that
kubernetes/argocd/lante-erp-application.yaml's `spec.source.path` never reaches. `kubectl get
cronjob -A` returned zero for 40 days. A diff of the file looked correct; the defect was *where
the file lived*, which a diff does not show and a reader has no reason to check.

This check computes what ArgoCD's Application manifests actually deploy — each Application's
`spec.source.path`, plus every chart that path's Chart.yaml pulls in via a local `file://`
dependency, recursively — and asserts every *.yaml/*.yml file under kubernetes/ is either inside
that reachable set or on the explicit ALLOWLIST below with a reason. The allowlist exists because
not everything under kubernetes/ is meant to be GitOps-managed: the three Application manifests
under kubernetes/argocd/ register themselves with ArgoCD via a one-time `kubectl apply` and can
never be "reachable" from their own path by definition, and a handful of cluster-bootstrap
manifests (cert-manager, the dashboard, a namespace, ServiceMonitors) are deliberately applied by
hand — see each file's own header comment. Anything else missing from both the reachable set and
the allowlist is either a genuine orphan or a manifest whose deployment path changed without
updating this list; either way it needs a human decision, not a silent pass.

Deliberately dynamic, not hardcoded, in the same places validate_service_infrastructure.py is
(sibling check, same failure class one layer down — Helm charts vs. the manifests inside them):
Application paths and Chart.yaml dependencies are discovered by walking the actual files, not
listed here by hand, so a new Application or a new subchart doesn't silently need this script
updated too — only genuinely new *standalone* manifests do, via ALLOWLIST.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
KUBERNETES_DIR = REPO_ROOT / "kubernetes"

# Files under kubernetes/ that are real manifests but deliberately not deployed via any ArgoCD
# Application path — each reason should point at the file's own header comment, which explains
# the manual-apply step in full. Keyed by path relative to REPO_ROOT.
#
# Application manifests themselves are NOT listed here: any file declaring `kind: Application` is
# unreachable-by-definition (it registers a deployment target; nothing deploys the registrar), so
# that is checked structurally in `check()` below rather than enumerated — a fourth Application
# manifest shouldn't need this list updated to avoid a false positive.
ALLOWLIST: dict[str, str] = {
    "kubernetes/cert-manager/cluster-issuer.yaml":
        "Manual apply per its own header comment (reuses an existing ClusterIssuer).",
    "kubernetes/dashboard/kubernetes-dashboard.yaml":
        "Manual apply per its own header comment (Kubernetes Dashboard, shared across clusters).",
    "kubernetes/monitoring/servicemonitors.yaml":
        "Manual apply per its own header comment. Tracked separately as effectively inert "
        "(docs/CURRENT-STATE.md notes it is scraped by nothing) — that is a monitoring-config "
        "gap, not the GitOps-reachability gap this check guards.",
    "kubernetes/namespaces/new-erp.yaml":
        "Redundant with lante-erp-application.yaml's `CreateNamespace=true` sync option; kept "
        "as a manual-apply fallback, not deployed by any Application path.",
    "kubernetes/namespaces/new-erp-staging.yaml":
        "Redundant with lante-erp-staging-application.yaml's `CreateNamespace=true` sync option "
        "(#221); kept as a manual-apply fallback, not deployed by any Application path.",
}

APPLICATION_KIND_RE = re.compile(r"^\s*kind:\s*Application\s*$", re.MULTILINE)
SOURCE_PATH_RE = re.compile(r"^\s*path:\s*(\S+)\s*$", re.MULTILINE)
CHART_DEP_LOCAL_REPO_RE = re.compile(r'repository:\s*"file://([^"]+)"')

SKIP_SUFFIXES = (".lock",)


def find_application_source_paths(root: Path = REPO_ROOT) -> set[Path]:
    """Every local `spec.source.path` declared by an ArgoCD Application manifest under kubernetes/."""
    paths: set[Path] = set()
    for manifest in sorted((root / "kubernetes").glob("**/*.yaml")):
        text = manifest.read_text(encoding="utf-8-sig")
        if not APPLICATION_KIND_RE.search(text):
            continue
        match = SOURCE_PATH_RE.search(text)
        if not match:
            continue  # e.g. velero-application.yaml — deploys an external chart, no local path
        candidate = (root / match.group(1)).resolve()
        if candidate.is_dir():
            paths.add(candidate)
    return paths


def find_local_chart_dependencies(chart_dir: Path) -> set[Path]:
    """Directories a chart's Chart.yaml pulls in via a local `file://` dependency."""
    chart_yaml = chart_dir / "Chart.yaml"
    if not chart_yaml.exists():
        return set()
    deps: set[Path] = set()
    for rel in CHART_DEP_LOCAL_REPO_RE.findall(chart_yaml.read_text(encoding="utf-8-sig")):
        resolved = (chart_dir / rel).resolve()
        if resolved.is_dir():
            deps.add(resolved)
    return deps


def find_reachable_chart_dirs(root: Path = REPO_ROOT) -> set[Path]:
    """Every chart directory ArgoCD actually deploys — Application paths plus their local
    Chart.yaml dependencies, closed transitively (a dependency chart can itself depend on more
    local charts, though in practice this repo is only ever one level deep)."""
    frontier = find_application_source_paths(root)
    reachable: set[Path] = set()
    while frontier:
        chart_dir = frontier.pop()
        if chart_dir in reachable:
            continue
        reachable.add(chart_dir)
        frontier |= find_local_chart_dependencies(chart_dir) - reachable
    return reachable


def find_reachable_manifest_files(root: Path = REPO_ROOT) -> set[Path]:
    """Every *.yaml/*.yml file inside a reachable chart directory — the deployed surface."""
    files: set[Path] = set()
    for chart_dir in find_reachable_chart_dirs(root):
        files |= set(chart_dir.glob("**/*.yaml"))
        files |= set(chart_dir.glob("**/*.yml"))
    return files


def find_all_manifest_files(root: Path = REPO_ROOT) -> set[Path]:
    """Every *.yaml/*.yml file under kubernetes/, excluding lockfiles."""
    kube_dir = root / "kubernetes"
    if not kube_dir.exists():
        return set()
    files = set(kube_dir.glob("**/*.yaml")) | set(kube_dir.glob("**/*.yml"))
    return {f for f in files if f.suffix not in SKIP_SUFFIXES}


def check(root: Path = REPO_ROOT) -> list[str]:
    # Resolved once, up front, so every path built downstream (chart dirs discovered via an
    # Application's `path:` field, via a Chart.yaml dependency, or via a plain glob) shares the
    # same canonical prefix — otherwise a symlinked temp dir (e.g. macOS's /var -> /private/var)
    # makes an identical directory compare unequal to itself and the real chart silently reads
    # as unreachable.
    root = root.resolve()
    problems: list[str] = []

    all_files = find_all_manifest_files(root)
    if not all_files:
        return ["No manifests found under kubernetes/ at all — this checker needs updating "
                "to match a refactor."]

    reachable = find_reachable_manifest_files(root)
    if not reachable:
        return ["No manifest reachable from any ArgoCD Application path — this checker needs "
                "updating to match a refactor (or ArgoCD's Application manifests moved)."]

    for path in sorted(all_files):
        if path in reachable:
            continue
        rel = str(path.relative_to(root))
        if rel in ALLOWLIST:
            continue
        if APPLICATION_KIND_RE.search(path.read_text(encoding="utf-8-sig")):
            continue  # unreachable-by-definition — see the ALLOWLIST comment above
        problems.append(
            f"{rel}: not reachable from any ArgoCD Application's spec.source.path (directly, or "
            f"via a chart it depends on), and not on the ALLOWLIST in "
            f"scripts/ci/validate_kubernetes_manifest_reachability.py. Either wire it into a "
            f"chart an Application actually deploys, or add it to ALLOWLIST with a reason "
            f"explaining why it is deliberately applied by hand."
        )
    return problems


def main() -> int:
    problems = check(REPO_ROOT)
    if problems:
        print(f"::error::Found {len(problems)} orphaned Kubernetes manifest(s):")
        for p in problems:
            print(f"::error::{p}")
        return 1
    print("Every Kubernetes manifest is either reachable from an ArgoCD Application path or allowlisted.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
