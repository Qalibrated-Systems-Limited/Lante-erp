#!/usr/bin/env python3
"""
Statically checks every TenantDbConnectionInterceptor.cs in the repo for the security properties
that actually matter for tenant isolation. This is THE single point of failure for schema-per-tenant
routing (see scripts/ci's sibling test suite in ticketing/tests/TicketingService.Tests, which proves
these properties at runtime for the canonical copy) — this script's job is to guarantee every OTHER
copy (compliance/finance/fleet-service/hse/operations/reporting/stores/subcontracts, all copy-pasted
from ticketing's original per each file's own header comment) still has the same properties, since a
"quick fix" or refactor of just one copy is exactly how a regression like this would slip in.

Two categories of file, both legitimate (see licensing's own file-header comment for why it's
deliberately different — LicenseService.QaliCoreLicenseDbContext backs a genuine cross-tenant
platform catalog, and MUST NEVER resolve into a caller's tenant schema):

1. "dynamic" interceptors (the majority): must resolve schema from the JWT `schema` claim first,
   fall back to the `X-Tenant-Schema` header only when there's no claim, reject anything that isn't
   a bare lowercase identifier via a regex BEFORE it reaches the SQL string, and only then
   interpolate it into `SET search_path`.
2. "static" interceptors (currently only licensing): must set a hardcoded, caller-input-free
   search_path — no interpolation of anything request-derived at all.

Deliberately not hardcoded to any specific service list or count — discovers every
TenantDbConnectionInterceptor.cs under packages/microservices/** (plus user-service's differently
-named UserServiceTenantConnectionInterceptor.cs) and classifies each one based on whether its
connection-opening methods reference the HttpContext accessor at all, so it keeps working unmodified
as services are added, removed, or migrated between the two categories.

#217: a resolved tenant schema must be the ONLY thing on search_path (`SET search_path TO
"{schema}"`, nothing appended after it). A missing tenant table must fail loudly, not silently
resolve into a trailing `public` (or any other schema) still on the path — that already bit once, in
the opposite direction, when an empty per-tenant `licenses` table shadowed the real platform catalog
(see licensing's static interceptor). The no-claim branch resetting to an explicit `public` (or a
service's own non-tenant home schema, e.g. store-service's `public, stores`) is deliberate and fine —
only the schema-claimed branch must never carry a trailing fallback.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
EXPECTED_REGEX_PATTERN = r"\^\[a-z_\]\[a-z0-9_\]\*\$"
CLAIM_LOOKUP = 'FindFirst("schema")'
HEADER_LOOKUP = 'Headers["X-Tenant-Schema"]'
IS_MATCH_CALL = "IsMatch("
SEARCH_PATH_INTERPOLATION = 'SET search_path TO \\"'  # the interpolated ($"...") branch
TRAILING_FALLBACK_AFTER_SCHEMA = re.compile(r'\{schema\}\\"\s*,')  # e.g. {schema}", public


def find_interceptor_files(microservices_root: Path = REPO_ROOT / "packages/microservices") -> list[Path]:
    return sorted(
        microservices_root.glob("**/TenantDbConnectionInterceptor.cs")
    ) + sorted(
        microservices_root.glob("**/UserServiceTenantConnectionInterceptor.cs")
    )


def is_static_variant(text: str) -> bool:
    """A 'static' interceptor never consults the request at all — no HttpContext reference in its
    connection-opening bodies. (licensing's own regex/claim lookups were REMOVED entirely as part
    of the fix documented in its file header, so absence of those symbols is exactly the signal.)"""
    return "_httpContextAccessor" not in text and "HttpContext" not in text


def check_static_interceptor(path: Path, text: str) -> list[str]:
    problems = []
    if re.search(r'\$"[^"]*\{', text):
        problems.append(
            f"{path}: classified as a 'static' (no-HttpContext) interceptor, but its SQL contains "
            f"a string interpolation — a static interceptor must never build its search_path from "
            f"anything request-derived. If this service now needs real tenant routing, it should "
            f"use the standard dynamic pattern instead (see ticketing's copy)."
        )
    return problems


def check_dynamic_interceptor(path: Path, text: str) -> list[str]:
    problems = []

    if not re.search(EXPECTED_REGEX_PATTERN, text):
        problems.append(
            f"{path}: does not contain the expected safe-schema-name regex "
            f"(^[a-z_][a-z0-9_]*$) — has the identifier validation been weakened or removed?"
        )

    if CLAIM_LOOKUP not in text:
        problems.append(f"{path}: missing the JWT 'schema' claim lookup ({CLAIM_LOOKUP}).")
    if HEADER_LOOKUP not in text:
        problems.append(f"{path}: missing the X-Tenant-Schema header fallback ({HEADER_LOOKUP}).")

    if CLAIM_LOOKUP in text and HEADER_LOOKUP in text:
        claim_idx = text.index(CLAIM_LOOKUP)
        header_idx = text.index(HEADER_LOOKUP)
        if header_idx < claim_idx:
            problems.append(
                f"{path}: the X-Tenant-Schema header is looked up BEFORE the JWT schema claim. "
                f"This is the exact security property that must never regress: a client-suppliable "
                f"header must never take precedence over the caller's own authenticated JWT, or any "
                f"authenticated user could repoint their connection at another tenant's schema just "
                f"by setting a header."
            )

    if IS_MATCH_CALL in text and SEARCH_PATH_INTERPOLATION in text:
        is_match_idx = text.index(IS_MATCH_CALL)
        interpolation_idx = text.index(SEARCH_PATH_INTERPOLATION)
        if interpolation_idx < is_match_idx:
            problems.append(
                f"{path}: the interpolated SET search_path appears before the regex validation — "
                f"a resolved schema value must be validated BEFORE it can reach the SQL string, "
                f"never after."
            )
    elif IS_MATCH_CALL not in text:
        problems.append(f"{path}: no regex IsMatch(...) validation call found at all.")
    elif SEARCH_PATH_INTERPOLATION not in text:
        problems.append(
            f"{path}: no interpolated 'SET search_path TO \"...\"' branch found — expected a "
            f"claim/header-driven schema to actually be used somewhere."
        )

    if TRAILING_FALLBACK_AFTER_SCHEMA.search(text):
        problems.append(
            f"{path}: the schema-claimed branch appends something after \"{{schema}}\" in its "
            f"SET search_path — #217: a missing tenant table must fail loudly, not silently "
            f"resolve into a trailing fallback schema. Only the no-claim branch may reset to an "
            f"explicit public (or a service's own non-tenant home schema)."
        )

    return problems


def find_wiring_problems(microservices_root: Path = REPO_ROOT / "packages/microservices") -> list[str]:
    problems = []
    for path in find_interceptor_files(microservices_root):
        text = path.read_text()
        if is_static_variant(text):
            problems.extend(check_static_interceptor(path, text))
        else:
            problems.extend(check_dynamic_interceptor(path, text))
    return problems


def main() -> int:
    files = find_interceptor_files()
    if not files:
        print("::error::No TenantDbConnectionInterceptor.cs files found at all — script needs updating to match a refactor.")
        return 1

    problems = find_wiring_problems()
    if problems:
        print(f"::error::Found {len(problems)} tenant-isolation interceptor problem(s):")
        for p in problems:
            print(f"  - {p}")
        return 1

    print(f"OK — {len(files)} TenantDbConnectionInterceptor.cs files checked, all secure:")
    for f in files:
        print(f"  - {f.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
