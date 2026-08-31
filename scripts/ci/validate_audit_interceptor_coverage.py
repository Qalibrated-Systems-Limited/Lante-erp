#!/usr/bin/env python3
"""
Field-level audit coverage (#216) — which services record WHAT CHANGED, not just that an endpoint
was hit.

The gateway's audit middleware captures method/path/status/actor for every state-changing request
it proxies, but it never sees request/response bodies, so it cannot say "who changed this invoice's
amount, from what, to what". finance closed that gap with `FinanceAuditInterceptor`: an EF Core
`SaveChanges` interceptor that reads `ChangeTracker` entries for before/after values per property and
writes them in the same transaction as the change itself. stores, crm, fleet-service, compliance,
hse, licensing, subcontracts, ticketing and operations followed with the same shape (a
`<Service>AuditLog` entity plus a `<Service>AuditInterceptor : SaveChangesInterceptor`). operations
keeps its existing `CalibrationAuditLog` untouched alongside the new general interceptor — that one
is a purpose-built ISO-17025 event trail for a specific workflow, not a generic change log, and the
new interceptor explicitly excludes it rather than replacing it. Every other service still has only
the coarse gateway log.

This does not fail on every currently-uncovered service — that would just be #216 restated as a red
build. It fails on the two things that matter for keeping a rollout honest:

  1. A service in KNOWN_GAPS actually has coverage now and the entry was not removed (stale baseline —
     the same shape as validate_dead_entities.py's KNOWN_DEAD). The baseline can only shrink.
  2. A service NOT in KNOWN_GAPS has no coverage — i.e. a new service scaffolded after this check
     landed skipped the pattern silently.

Detection: a class in the service's own source (excluding tests) that extends SaveChangesInterceptor.
This does not verify the interceptor is actually wired into a DbContext's AddInterceptors call, or that
it is tested — only that the service has adopted the pattern at all. That is deliberately weaker than
#216's full ask; it exists to make the rollout's progress visible in CI, not to re-litigate each
service's implementation.

Usage:  python3 scripts/ci/validate_audit_interceptor_coverage.py
Exit:   0 clean, 1 a stale baseline entry or an untracked gap, 2 could not parse.
"""
from __future__ import annotations

import glob
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

INTERCEPTOR_RE = re.compile(r"class\s+\w+.*:\s*.*\bSaveChangesInterceptor\b")

# Services with no field-level audit interceptor yet, in rough tier-1 value order. Checked in BOTH
# directions — an entry that gains coverage is an ERROR until it is removed, same convention as
# validate_dead_entities.py's KNOWN_DEAD. user-service is the identity/RBAC service and arguably the
# highest-value remaining target (see #217's "who changed this user's role" gap) but also the
# riskiest to touch: two DbContext types (LanteUserServiceDbContext, DI-registered, plus TenantDbContext,
# hand-constructed in TenantAuthenticator/UserDirectory/TenantProvisioningService, none of which
# currently take an IHttpContextAccessor) mean real wiring work, not a drop-in copy of the finance/
# stores shape. Do that one deliberately, not as part of extending this baseline.
KNOWN_GAPS = {
    "user-service":  "#216 — identity/RBAC service, two DbContext types, needs its own wiring plan",
    "hr":            "#216/#392 — HrAuditLog's business-event enum + narrative Detail is richer than a "
                     "generic diff, and CreateAsync/UpdateAsync auto-save per call, so a fallback "
                     "interceptor would double-log every write rather than only the forgotten ones; "
                     "needs a unit-of-work refactor first",
    "procurement":   "#216/#392 — same shape as hr: ProcurementAuditLog's AsrAuditAction narrative "
                     "can't be replicated generically, and per-call auto-save blocks a safe fallback "
                     "interceptor until there's a real unit-of-work",
    "reporting":     "#216 — read-only aggregation service; revisit if that changes",
}


def die(msg: str) -> None:
    print(f"::error::{msg}")
    sys.exit(2)


def service_of(path: str) -> str:
    return path.split("/src/")[0].split("/")[-1]


def all_services(root: str = REPO) -> set[str]:
    services: set[str] = set()
    for path in glob.glob(os.path.join(root, "packages/microservices/**/src"), recursive=True):
        if "/obj/" in path or "/bin/" in path:
            continue
        services.add(service_of(path + "/x"))
    if not services:
        die("No service src/ directories found under packages/microservices — the layout moved, and "
            "this must not pass silently while covering nothing.")
    return services


def covered_services(root: str = REPO) -> set[str]:
    covered: set[str] = set()
    pattern = os.path.join(root, "packages/microservices/**/src/**/*.cs")
    for path in sorted(glob.glob(pattern, recursive=True)):
        if "/obj/" in path or "/bin/" in path or "/tests/" in path:
            continue
        with open(path, encoding="utf-8", errors="ignore") as fh:
            if INTERCEPTOR_RE.search(fh.read()):
                covered.add(service_of(path))
    return covered


def main() -> int:
    services = all_services()
    covered = covered_services()
    gaps = services - covered

    print(f"Services: {len(services)} | with a SaveChanges audit interceptor: {len(covered)}")

    untracked = sorted(gaps - set(KNOWN_GAPS))
    stale = sorted(set(KNOWN_GAPS) - gaps)

    for svc in stale:
        print(f"::error::{svc} is listed in KNOWN_GAPS but now has a SaveChanges audit interceptor — "
              f"remove the entry. The baseline can only shrink, or it stops describing anything true.")

    if gaps & set(KNOWN_GAPS):
        print(f"\n{len(gaps & set(KNOWN_GAPS))} known gap(s), tracked (not a failure):")
        for svc in sorted(gaps & set(KNOWN_GAPS)):
            print(f"  {svc:16} {KNOWN_GAPS[svc]}")

    if untracked:
        print(f"\n{len(untracked)} service(s) with no audit interceptor and no baseline entry:\n")
        for svc in untracked:
            print(f"  {svc}")
        print("\nEither add a SaveChanges interceptor following FinanceAuditInterceptor's shape, or add "
              "an entry to KNOWN_GAPS here explaining why not yet. See #216.")
        print("::error::New untracked audit-coverage gap detected. See #216.")

    if untracked or stale:
        return 1
    print("No new untracked gaps.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
