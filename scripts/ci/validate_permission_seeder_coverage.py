#!/usr/bin/env python3
"""
Fail when a permission string is checked somewhere (`HasClaim("permission", "x")` or
`[Authorize(Policy = "x")]` / `[Authorize(Policy = "Permission:x")]`) but never seeded, so no role
could ever hold it and the check can never pass for anyone but a system.admin bypass.

This is the exact bug shape #282 found: operations.read.dept and operations.read.all were enforced
in three operations-service controllers since that service existed, but DatabaseSeeder.cs never
created them as Permission rows, so the department and org-wide visibility tiers were unreachable —
and, worse, a whole frontend page (Negligence) was hidden from every non-admin because its route and
nav item are gated on operations.read.dept. A permission that looks wired up because the code that
checks it reads correctly is the dangerous case; a checker that only verifies "does this compile" or
"is this permission used somewhere" would still miss it, because the code IS used and DOES compile —
what's missing is the seed row that would let a role ever hold it.

DatabaseSeeder.cs (packages/microservices/masterdata/user-service/.../DatabaseSeeder.cs) is the one
place every real permission string is created, across every service — not each service's own
handler, which only enforces strings it assumes exist. That is the authority this script reads.

Scope: only permission-shaped strings (lowercase words joined by dots) are considered — this
deliberately misses a policy name that isn't of that shape, but every real permission in this repo
is, and a non-permission Policy name would be a much stranger bug to have and likely caught some
other way.

Pre-existing gaps go in KNOWN_MISSING, tracked in a follow-up issue, so this can be enabled without
requiring every service to be fixed first. Like validate_permission_hierarchy.py, the baseline is
checked in both directions: a baselined entry that got seeded is now an error too, not a silent
pass — otherwise this list only ever grows and nobody notices it should have shrunk.

Usage:  python3 scripts/ci/validate_permission_seeder_coverage.py
Exit:   0 clean, 1 new gap found (or a stale baseline entry), 2 could not parse.
"""
from __future__ import annotations

import glob
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SERVICES_ROOT = os.path.join(REPO, "packages/microservices")
SEEDER = os.path.join(
    REPO,
    "packages/microservices/masterdata/user-service/src/UserService.Infrastructure/Data/DatabaseSeeder.cs",
)

POLICY_RE = re.compile(r'\[Authorize\(Policy\s*=\s*"(?:Permission:)?([a-z][a-z0-9_.]*)"\)\]', re.IGNORECASE)
CLAIM_RE = re.compile(r'HasClaim\("permission",\s*"([a-z][a-z0-9_.]*)"\)', re.IGNORECASE)
SEED_ROW_RE = re.compile(r'^\s*\(Perm[A-Za-z0-9_]+,\s*"([a-z][a-z0-9_.]*)"', re.IGNORECASE)

# Permission -> issue tracking it. Found while building this checker (running it for real turned up
# 22 hits beyond the 2 #282 itself fixed) — filed as its own issue rather than folded into #282,
# since fixing hr/crm/procurement's gaps needs the same "which role should hold this" research #282
# did for operations, service by service.
KNOWN_MISSING = {
    # Emptied by #293, which seeded all 22 — hr.* (9), crm.* (6), procurement.* (4), calibration.* (2)
    # and reports.schedule. Every one of them was enforced by its service and present in no catalog,
    # leaving HR, CRM and procurement reachable only by system.admin.
    #
    # This baseline going stale turned main red for two commits, and that is worth recording rather
    # than quietly deleting: TWO independent checks were built for this class within hours of each
    # other, each with its own baseline. I emptied the one in validate_permission_hierarchy.py and did
    # not know this one existed, so the seeding landed while half the bookkeeping did not. The
    # both-directions design caught it — which is the point — but on main rather than in review. See
    # the note at the top of validate_permission_hierarchy.py about consolidating the two.
}


def _is_comment(line: str) -> bool:
    stripped = line.strip()
    return stripped.startswith("//") or stripped.startswith("*") or stripped.startswith("///")


def find_referenced_permissions(root: str) -> set[str]:
    referenced: set[str] = set()
    pattern = os.path.join(root, "**", "*.cs")
    for path in glob.glob(pattern, recursive=True):
        if "/Migrations/" in path or "/obj/" in path or "/bin/" in path:
            continue
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                if _is_comment(line):
                    continue
                for m in POLICY_RE.finditer(line):
                    referenced.add(m.group(1))
                for m in CLAIM_RE.finditer(line):
                    referenced.add(m.group(1))
    return referenced


def find_seeded_permissions(seeder_path: str) -> set[str]:
    seeded: set[str] = set()
    with open(seeder_path, encoding="utf-8") as fh:
        for line in fh:
            m = SEED_ROW_RE.match(line)
            if m:
                seeded.add(m.group(1))
    return seeded


def compare(referenced: set[str], seeded: set[str], known_missing: dict[str, str]) -> tuple[set[str], set[str]]:
    """Returns (new_gaps, stale_baseline_entries)."""
    unseeded = referenced - seeded
    new_gaps = unseeded - set(known_missing)
    stale_baseline = set(known_missing) - unseeded
    return new_gaps, stale_baseline


def main() -> int:
    if not os.path.isfile(SEEDER):
        print(f"Could not find DatabaseSeeder.cs at {SEEDER}", file=sys.stderr)
        return 2

    referenced = find_referenced_permissions(SERVICES_ROOT)
    seeded = find_seeded_permissions(SEEDER)
    if not referenced or not seeded:
        print(f"Parsed suspiciously little (referenced={len(referenced)}, seeded={len(seeded)}) "
              "— treating as a parse failure rather than trusting an empty result.", file=sys.stderr)
        return 2

    new_gaps, stale_baseline = compare(referenced, seeded, KNOWN_MISSING)

    print(f"permission strings referenced: {len(referenced)} | seeded: {len(seeded)} | "
          f"known pre-existing gaps: {len(KNOWN_MISSING)}")

    failed = False

    if new_gaps:
        failed = True
        print("\nNEW permission(s) referenced but never seeded — no role can ever hold these:")
        for p in sorted(new_gaps):
            print(f"  {p}")

    if stale_baseline:
        failed = True
        print("\nKNOWN_MISSING entries that are no longer missing — remove them from the baseline:")
        for p in sorted(stale_baseline):
            print(f"  {p}  (tracked as {KNOWN_MISSING[p]})")

    if not failed:
        print("\nNo new unseeded permissions, and the baseline is still accurate.")
        return 0

    return 1


if __name__ == "__main__":
    sys.exit(main())
