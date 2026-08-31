#!/usr/bin/env python3
"""
Every service whose BaseEntity carries IsDeleted must have at least one place that sets it.

Same shape as #276 (a guard that reads as protection and never fires), found while building that
issue's item 1. 14 services declare `IsDeleted` on `BaseEntity` as the platform's soft-delete
convention. 12 wire it correctly: a `GenericRepository.DeleteAsync` (or equivalent) sets
`IsDeleted = true`, most pair it with a `RestoreAsync` setting it back to `false`. Two do not
(#332):

  - finance-service: 21 `HasQueryFilter(x => !x.IsDeleted)` calls, zero writes anywhere, zero
    `[HttpDelete]` endpoints at all. The filters are inert — nothing can ever set the column they
    check.
  - hr-service: ~100 guards and query filters assume soft delete, but
    `HrService.Infrastructure/Repositories/GenericRepository.cs` does `DbSet.Remove(entity)` — a
    real, permanent delete. IsDeleted never becomes true there either, just for a different reason.

Deliberately narrow rather than a generic "any guard field is ever unwritten" scanner: an early,
wider version of this idea (any Is/Has/Can-prefixed bool read in a conditional) flagged mapper-driven
writes (`_mapper.Map(dto, entity)` copying a DTO's IsActive onto the entity, invisible to a textual
`Name = value` search) and seed-only variation (DatabaseSeeder literals that legitimately differ per
row) as false positives. IsDeleted's write is structurally uniform across every service that has it
right — one `GenericRepository`, one line — so this scoped version has none of that noise. Item 1's
general form remains open; widen this rather than write a second, noisier check if another
uniform-enough pattern turns up.

hr-service was fixed the same day (`GenericRepository.DeleteAsync` now sets `IsDeleted = true`
instead of `DbSet.Remove(entity)`, plus a `RestoreAsync` to match every other service's interface
shape). finance-service resolved the other way: #332 decided finance has no delete path anywhere
in the service and none was worth adding, so `IsDeleted` was removed entirely — the column, all 21
query filters, the property on `BaseEntity`. finance no longer appears in
`services_with_is_deleted()` at all, which is why the baseline below is empty rather than carrying
a finance entry.

Usage:  python3 scripts/ci/validate_soft_delete_reachability.py
Exit:   0 clean, 1 a new service with a dead IsDeleted (or a stale baseline entry), 2 could not parse.
"""
from __future__ import annotations

import glob
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BASE_ENTITY_GLOB = os.path.join(REPO, "packages/microservices/**/BaseEntity.cs")
SRC_GLOB = os.path.join(REPO, "packages/microservices/**/*.cs")

DECL_RE = re.compile(r"public\s+bool\??\s+IsDeleted\s*\{\s*get;")
WRITE_RE = re.compile(r"\bIsDeleted\s*=\s*(?!=)(true|false)\b")

# Checked in BOTH directions, like every other baseline here — an entry that becomes written is an
# ERROR until removed, so this can only shrink. Empty as of #332's finance resolution (removed the
# column rather than wiring it up); stays empty until another service turns up in this state.
KNOWN_DEAD = {}


def die(msg: str) -> None:
    print(f"::error::{msg}")
    sys.exit(2)


def service_of(path: str) -> str:
    return path.split("/src/")[0].split("/")[-1]


def services_with_is_deleted() -> set[str]:
    services: set[str] = set()
    for path in sorted(glob.glob(BASE_ENTITY_GLOB, recursive=True)):
        if "/obj/" in path or "/bin/" in path:
            continue
        with open(path, encoding="utf-8", errors="ignore") as fh:
            if DECL_RE.search(fh.read()):
                services.add(service_of(path))
    if not services:
        die("No BaseEntity.cs declares IsDeleted. Either the convention moved or the `public bool "
            "IsDeleted { get; ...` form changed — this must not pass silently, or the check would "
            "cover nothing while reporting success.")
    return services


def has_write(service: str) -> bool:
    for path in sorted(glob.glob(SRC_GLOB, recursive=True)):
        if "/obj/" in path or "/bin/" in path or "/src/" not in path:
            continue
        if service_of(path) != service:
            continue
        low = path.lower()
        # BaseEntity.cs's own `= false` default isn't a write — every instance gets it regardless
        # of any delete ever happening. Migrations/Designer only declare the column exists.
        if low.endswith("/baseentity.cs") or "migration" in low or "designer.cs" in low:
            continue
        with open(path, encoding="utf-8", errors="ignore") as fh:
            if WRITE_RE.search(fh.read()):
                return True
    return False


def main() -> int:
    services = services_with_is_deleted()
    dead = {svc for svc in services if not has_write(svc)}

    print(f"services with IsDeleted on BaseEntity: {len(services)} | with no write anywhere: {len(dead)}")

    new = sorted(dead - set(KNOWN_DEAD))
    revived = sorted(set(KNOWN_DEAD) - dead)

    for svc in revived:
        print(f"::error::{svc} is listed in KNOWN_DEAD but now writes IsDeleted — delete the entry. "
              f"The baseline can only shrink, or it stops describing anything true.")

    if dead & set(KNOWN_DEAD):
        print(f"\n{len(dead & set(KNOWN_DEAD))} known dead IsDeleted convention(s), tracked (not a failure):")
        for svc in sorted(dead & set(KNOWN_DEAD)):
            print(f"  {svc:16} {KNOWN_DEAD[svc]}")

    if new:
        print(f"\n{len(new)} service(s) with IsDeleted on BaseEntity and no write anywhere:\n")
        for svc in new:
            print(f"  {svc}")
        print("\nEither wire a real delete path (copy GenericRepository's DeleteAsync from a working "
              "service, e.g. compliance or hse), or remove IsDeleted, its query filters and its "
              "guards and say so. A soft-delete convention nothing implements reads as a working "
              "feature to whoever finds it next — that is what #332 already was twice.")
        print("::error::New unwritten IsDeleted found. See #276.")

    if revived:
        print(f"\n{len(revived)} stale KNOWN_DEAD entr(y/ies). Remove them.")

    if new or revived:
        return 1
    print("No new dead IsDeleted convention.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
