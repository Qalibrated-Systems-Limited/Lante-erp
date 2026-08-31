#!/usr/bin/env python3
"""
Guards against the exact class of bug found and fixed on 2026-07-22: a schema-per-tenant service
ships a real, request-scoped tenant connection interceptor that binds every request's Postgres
connection to the caller's tenant schema via search_path — but the DbContext that interceptor is
attached to is wired directly to DefaultConnection with no AppConnection split at all, so every
authenticated request runs as the postgres admin/superuser role instead of the least-privilege
qalicore_app role.

This happened to "finance" (a new service, never wired up) and, far more seriously, to
"user-service" (the platform's identity/login service — EVERY authenticated request ran as the
postgres superuser from day one, found only via a manual audit, not any automated check).

Deliberately generic: finds every "*TenantConnectionInterceptor.cs" file (matches both the standard
TenantDbConnectionInterceptor.cs name used by most services and user-service's differently-named
UserServiceTenantConnectionInterceptor.cs), derives that service's root directory from the path
segment(s) before its "src" folder, and requires at least one .cs file anywhere under that service
root to reference GetConnectionString("AppConnection"). No hardcoded service list — keeps working
unmodified as services are added, renamed, or moved.

This only checks that an AppConnection fallback exists somewhere in the service — it can't prove
the interceptor's own DbContext is the one actually using it (that would need real data-flow
analysis). It's a floor, not a full proof: still catches the actual bug class (an AppConnection
reference is completely absent), which is what both real incidents looked like.
"""
from __future__ import annotations

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
MICROSERVICES_ROOT = REPO_ROOT / "packages/microservices"
APP_CONNECTION_MARKER = 'GetConnectionString("AppConnection")'


def find_interceptor_files(microservices_root: Path = MICROSERVICES_ROOT) -> list[Path]:
    return sorted({
        *microservices_root.glob("**/TenantDbConnectionInterceptor.cs"),
        *microservices_root.glob("**/*TenantConnectionInterceptor.cs"),
    })


def service_root_for(interceptor_path: Path, microservices_root: Path = MICROSERVICES_ROOT) -> Path:
    """A service's root directory is everything before its 'src' segment, e.g.
    "finance/src/FinanceService.Infrastructure/Data/TenantDbConnectionInterceptor.cs" -> "finance",
    "masterdata/user-service/src/.../UserServiceTenantConnectionInterceptor.cs" -> "masterdata/user-service"."""
    rel = interceptor_path.relative_to(microservices_root)
    parts = rel.parts
    if "src" in parts:
        return microservices_root / Path(*parts[:parts.index("src")])
    return microservices_root / parts[0]


def has_app_connection_reference(service_root: Path) -> bool:
    for cs_file in service_root.glob("**/*.cs"):
        try:
            if APP_CONNECTION_MARKER in cs_file.read_text():
                return True
        except (UnicodeDecodeError, OSError):
            continue
    return False


def find_problems(microservices_root: Path = MICROSERVICES_ROOT) -> list[str]:
    problems = []
    seen_roots: set[Path] = set()
    for interceptor in find_interceptor_files(microservices_root):
        service_root = service_root_for(interceptor, microservices_root)
        if service_root in seen_roots:
            continue
        seen_roots.add(service_root)

        if not has_app_connection_reference(service_root):
            rel = service_root.relative_to(microservices_root)
            problems.append(
                f"'{rel}' has a live tenant connection interceptor ({interceptor.name}) binding "
                f"requests to a per-tenant schema, but no file anywhere under it references "
                f'GetConnectionString("AppConnection") — its runtime queries run on whatever '
                f"DefaultConnection is (the postgres admin/superuser role), not a least-privilege "
                f"connection. Add an AppConnection ?? DefaultConnection fallback for the runtime "
                f"DbContext, keeping DefaultConnection for migrations/provisioning only."
            )
    return problems


def main() -> int:
    problems = find_problems()
    if problems:
        print("::error::Found service(s) with no least-privilege runtime connection:")
        for p in problems:
            print(f"  - {p}")
        return 1

    print("OK — every service with a tenant connection interceptor has an AppConnection least-privilege path.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
