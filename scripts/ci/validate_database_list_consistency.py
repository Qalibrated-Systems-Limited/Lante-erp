#!/usr/bin/env python3
"""
Guards against the exact class of bug found on 2026-08-18: three separate files each hardcoded
their own list of the live tenant databases, and none of them agreed. The Postgres `initdb`
bootstrap script (kubernetes/helm-charts/lante-erp-platform/values.yaml) only created 11 of the
14 live databases — `lante_crm`, `lante_hr`, `lante_procurement` existed live but were added
out-of-band and were missing from a from-scratch bootstrap entirely. `generate-secrets.sh` was
missing 5 connection-string keys for the same reason. A backup CronJob (since deleted, replaced
by a dynamic pg_database-driven job) covered only 5 of the 14.

The `initdb` script's `DATABASES=` list is the one file where a static list is genuinely
unavoidable — it runs before any database exists to query. Everything else either queries
`pg_database` at runtime (preferred, and what the backup jobs now do) or, if it must hardcode
connection strings (generate-secrets.sh, which builds them from a shell variable before any
Kubernetes Secret exists), gets checked here against the initdb list as the single source of
truth. This script also does a broad sweep for *any other* file that hardcodes 3+ `lante_*`
database names, so a future file written the old way (like the deleted backup CronJob) gets
caught before merge rather than 40 days after.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
VALUES_YAML = REPO_ROOT / "kubernetes/helm-charts/lante-erp-platform/values.yaml"
GENERATE_SECRETS_SH = REPO_ROOT / "kubernetes/secrets/generate-secrets.sh"

# Files allowed to hardcode a lante_* database list — the initdb script (unavoidable, checked as
# the source of truth) and generate-secrets.sh (checked explicitly against it below). Any other
# file matching the stray-list heuristic is new and needs a look.
ALLOWED_HARDCODED_LIST_FILES = {VALUES_YAML, GENERATE_SECRETS_SH}

DB_NAME_RE = re.compile(r"\blante_[a-z]+\b")


def find_source_of_truth_databases(values_yaml: Path = VALUES_YAML) -> set[str]:
    text = values_yaml.read_text()
    m = re.search(r'DATABASES="([^"]+)"', text)
    if not m:
        print(f"::error::Could not find DATABASES=\"...\" in {values_yaml} — script needs updating to match a refactor.")
        sys.exit(1)
    return set(m.group(1).split())


def find_generate_secrets_databases(generate_secrets_sh: Path = GENERATE_SECRETS_SH) -> set[str]:
    text = generate_secrets_sh.read_text()
    return set(re.findall(r"Database=(lante_[a-z]+)", text))


def check_no_stray_hardcoded_lists(
    kubernetes_dir: Path = REPO_ROOT / "kubernetes",
    repo_root: Path = REPO_ROOT,
    allowed_files: set[Path] = ALLOWED_HARDCODED_LIST_FILES,
) -> list[str]:
    """Scans every file under kubernetes/ for a line naming 3+ distinct lante_* databases outside
    the two files that are explicitly allowed to. Three is the threshold because a line naming
    one or two db names is usually an unrelated comment/example, not a real enumeration."""
    problems = []
    for path in kubernetes_dir.rglob("*"):
        if not path.is_file() or path in allowed_files:
            continue
        try:
            text = path.read_text()
        except (UnicodeDecodeError, OSError):
            continue
        for lineno, line in enumerate(text.splitlines(), start=1):
            names = set(DB_NAME_RE.findall(line))
            if len(names) >= 3:
                try:
                    display_path = path.relative_to(repo_root)
                except ValueError:
                    display_path = path
                problems.append(
                    f"{display_path}:{lineno} hardcodes {len(names)} database names "
                    f"({sorted(names)}) — query pg_database dynamically instead, or if a static list "
                    f"is genuinely unavoidable here, add this file to ALLOWED_HARDCODED_LIST_FILES in "
                    f"{Path(__file__).name} and check it against the initdb list explicitly."
                )
    return problems


def find_consistency_problems(
    values_yaml: Path = VALUES_YAML,
    generate_secrets_sh: Path = GENERATE_SECRETS_SH,
    kubernetes_dir: Path = REPO_ROOT / "kubernetes",
    repo_root: Path = REPO_ROOT,
) -> list[str]:
    truth = find_source_of_truth_databases(values_yaml)
    secrets_dbs = find_generate_secrets_databases(generate_secrets_sh)

    problems = []
    missing_from_secrets = sorted(truth - secrets_dbs)
    if missing_from_secrets:
        problems.append(
            f"{generate_secrets_sh.name} is missing a connection string for: {missing_from_secrets} "
            f"(present in {values_yaml.name}'s DATABASES= list but not built anywhere here)"
        )
    extra_in_secrets = sorted(secrets_dbs - truth)
    if extra_in_secrets:
        problems.append(
            f"{generate_secrets_sh.name} builds connection strings for {extra_in_secrets}, which "
            f"aren't in {values_yaml.name}'s DATABASES= list — either the list is stale or these "
            f"databases are no longer real"
        )

    problems.extend(check_no_stray_hardcoded_lists(kubernetes_dir, repo_root, {values_yaml, generate_secrets_sh}))
    return problems


def main() -> int:
    problems = find_consistency_problems()

    if problems:
        print("::error::Database list is inconsistent across the repo:")
        for p in problems:
            print(f"  - {p}")
        return 1

    truth = find_source_of_truth_databases()
    print(f"OK — {len(truth)} databases consistent across {VALUES_YAML.name} and {GENERATE_SECRETS_SH.name}, no stray hardcoded lists found: {sorted(truth)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
