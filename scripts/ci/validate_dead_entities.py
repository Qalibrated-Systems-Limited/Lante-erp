#!/usr/bin/env python3
"""
An entity with a table, a migration and a DbSet, that no service code ever writes or queries.

This is one form of the pattern in #276: something committed, correct-looking, and never running. It
found `FinanceAuditLog` — finance had a dedicated audit table from InitialCreate that had never recorded
a row (#285) — and `ForexRevaluationLog`, a forex-revaluation table shipped to every tenant schema that
nothing has ever written (#226). Both read as working features to anyone looking at the schema.

Detection is deliberately generous about what counts as a USE, because the first pass of this check
flagged 29 and the real answer was 8. Every drop was a false-positive class, not a fix:

  1. Construction is rarely `new Entity`. PayslipLine looked dead; it is built by a `Line(...)` helper
     into a `new List<PayslipLine>()`. AutoMapper and cascade-save hide others.
  2. Entities are used via the DbSet PROPERTY, not the type. Currency and TaxCategory looked dead until
     `_db.Currencies` was searched for.
  3. Navigation properties are singular and unrelated to the DbSet name. AccountType looked dead; it is
     `a.AccountType` everywhere.

Shipped at pass one this would have reported ~70% false positives into CI and been switched off inside a
week. The patterns below are the residue of that, and should be widened rather than narrowed if a real
use is ever reported as dead.

Credit: the detection heuristics are @new-erp-ca's, handed over rather than landed because scripts/ci is
this session's lane.

Usage:  python3 scripts/ci/validate_dead_entities.py
Exit:   0 clean, 1 a new dead entity (or a stale baseline entry), 2 could not parse.
"""
from __future__ import annotations

import collections
import glob
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CONTEXTS = os.path.join(REPO, "packages/microservices/**/*DbContext.cs")
SOURCES = os.path.join(REPO, "packages/microservices/**/*.cs")

# Entities that already had a table and no writer when this check landed. Each is a real finding, not a
# suppression: the table exists in every tenant schema and reads as a working feature.
#
# Checked in BOTH directions — an entry that becomes used is an ERROR until it is deleted. A baseline
# that is only ever suppressed becomes permanent and starts hiding a different problem under the same
# name. It can only shrink. FinanceAuditLog was on this list and came off when #285 wired it up.
#
# #330's other six were resolved 2026-08-20: DriverActivity, WorkflowRule, JournalEntryAttachment,
# PendingVerification (operations copy) and ServiceRequestInstrument (ticketing copy) were deleted —
# entity, DbSet and table — as abandoned stubs with no writer or reader anywhere. PerformanceMetrics
# was the one exception worth wiring up rather than deleting, and #339 did: PerformanceService now
# implements IPerformanceService for real (Assignment/AssignedTechnician/ServiceReport/Requisition
# aggregation), exposed via PerformanceController. Off the baseline as of that PR.
KNOWN_DEAD = {
    ("finance", "ForexRevaluationLog"):           "#226 — revaluation is absent, not incomplete",
}


def die(msg: str) -> None:
    print(f"::error::{msg}")
    sys.exit(2)


def service_of(path: str) -> str:
    return path.split("/src/")[0].split("/")[-1]


def parse_dbsets(pattern: str) -> dict[str, set[tuple[str, str]]]:
    """entity type -> {(service, DbSet property name)}"""
    decls: dict[str, set[tuple[str, str]]] = collections.defaultdict(set)
    for path in sorted(glob.glob(pattern, recursive=True)):
        if "/obj/" in path or "/bin/" in path:
            continue
        with open(path, encoding="utf-8", errors="ignore") as fh:
            body = fh.read()
        svc = service_of(path)
        for m in re.finditer(r"DbSet<(\w+)>\s+(\w+)", body):
            decls[m.group(1)].add((svc, m.group(2)))
    if not decls:
        die("No DbSet declarations found. Either the contexts moved or the DbSet<T> Name form changed — "
            "this must not pass silently, or the check would cover nothing while reporting success.")
    return decls


def load_sources(pattern: str) -> dict[str, list[str]]:
    """service -> the bodies of its source files, excluding the places a mere DEFINITION lives."""
    src: dict[str, list[str]] = collections.defaultdict(list)
    for path in sorted(glob.glob(pattern, recursive=True)):
        if "/obj/" in path or "/bin/" in path or "/src/" not in path:
            continue
        low = path.lower()
        # Migrations, seeders and the DbContext declare the table; declaring is not using. The entity's
        # own file under Entities/ is its definition, likewise.
        if "migration" in low or "seeder" in low or "dbcontext" in low or "/entities/" in low:
            continue
        with open(path, encoding="utf-8", errors="ignore") as fh:
            src[service_of(path)].append(fh.read())
    return src


def is_used(entity: str, dbset: str, bodies: list[str]) -> bool:
    patterns = [
        rf"\bnew\s+{entity}\b",        # direct construction
        rf"<{entity}>",                # single-arg generics: IGenericRepository<>, List<>, ICollection<>
                                       # NOT multi-arg: CreateMap<Entity, Dto> is deliberately excluded.
                                       # A mapping profile entry proves intent, not use — operations/
                                       # PerformanceMetrics has exactly one and no query, and widening
                                       # this would quietly drop it from the list.
        rf"\b{entity}\s+\w+\s*=",      # local or field declaration
        rf"ICollection<{entity}>",     # navigation collection
        rf"\.{dbset}\b",               # _db.Currencies — class 2 above
        rf"\b{dbset}\.",
        rf"\.{entity}\b",              # a.AccountType — class 3 above
    ]
    return any(re.search(p, body) for p in patterns for body in bodies)


def main() -> int:
    decls = parse_dbsets(CONTEXTS)
    src = load_sources(SOURCES)

    total = sum(len(v) for v in decls.values())
    dead = {(svc, ent) for ent, places in decls.items()
            for svc, prop in places if not is_used(ent, prop, src.get(svc, []))}

    print(f"DbSet declarations: {total} | with no writer or reader in service src: {len(dead)}")

    new = sorted(dead - set(KNOWN_DEAD))
    revived = sorted(set(KNOWN_DEAD) - dead)

    for svc, ent in revived:
        print(f"::error::{svc}/{ent} is listed in KNOWN_DEAD but is now used — delete the entry. The "
              f"baseline can only shrink, or it stops describing anything true.")

    if dead & set(KNOWN_DEAD):
        print(f"\n{len(dead & set(KNOWN_DEAD))} known dead entit(y/ies), tracked (not a failure):")
        for svc, ent in sorted(dead & set(KNOWN_DEAD)):
            print(f"  {svc:16} {ent:30} {KNOWN_DEAD[(svc, ent)]}")

    if new:
        print(f"\n{len(new)} NEW entit(y/ies) with a table and no code that touches it:\n")
        for svc, ent in new:
            print(f"  {svc:16} {ent}")
        print("\nEither wire it up, or delete the entity, the DbSet and the table and say so. A dormant "
              "table reads as a working feature to whoever finds it next — that is what #285 and #226 "
              "were. If this is a false positive, WIDEN the patterns in is_used rather than adding a "
              "baseline entry; the pattern list exists because 21 of the first 29 findings were false.")
        print("::error::New dead entity detected. See #276.")

    if revived:
        print(f"\n{len(revived)} stale KNOWN_DEAD entr(y/ies). Remove them.")

    if new or revived:
        return 1
    print("No new dead entities.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
