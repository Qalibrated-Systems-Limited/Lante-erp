#!/usr/bin/env python3
"""
Guards the runtime app role used when provisioning a new tenant schema.

The bug this was written for (2026-08-18, issue #196): every service's
TenantProvisioningService grants the least-privilege runtime role access to the schema it
has just created, reading the role name from configuration with a hardcoded fallback:

    var appRole = _configuration["ProvisioningAppRole"] ?? "<fallback>";

`ProvisioningAppRole` is set nowhere — not in the Helm values, not in docker-compose — so
every service uses its fallback, and the fallbacks had drifted. Seven of fourteen said
`lante_app`, a role that has never existed; the other seven said `qalicore_app`, which is
the role `generate-secrets.sh` actually creates and which every service connects as at
request time via AppConnection.

The failure is loud rather than silent — GrantAppRoleAsync checks pg_roles first, and the
caller logs at Error and returns ProvisioningResult(false, ...) — so onboarding a company
visibly fails for those seven services rather than quietly producing a broken tenant. That
is better than it could have been, but it still means half the platform cannot provision.

The drift almost certainly came from the QaliCore -> Lante rebrand: `Deployment/DEPLOYMENT.md`
claimed the role had "already been renamed to lante_app", which was never true. The comment
and the fallback string were updated to match the doc; the Postgres role was not.

This check asserts every fallback matches the role generate-secrets.sh actually creates, so
a future rename has to change both or fail the build. Renaming the role for real is a
privilege migration, not a string change — see #197.

UPDATE (issue #198): this check originally only globbed `TenantProvisioningService.cs` and only
matched the literal `_configuration["ProvisioningAppRole"]` spelling. That missed two real
fallbacks that read the same key under a different variable name in a differently-named file —
`ReportingService.Api/Program.cs`'s `config["ProvisioningAppRole"]` (correct value, caught this
check's own drift-detection blind spot rather than a real bug) and, live and wrong until fixed in
the same PR that widened this check, `UserService.Api/Program.cs`'s
`builder.Configuration["ProvisioningAppRole"] ?? "lante_app"` — the exact #196 bug this script
exists to catch, sitting in a file the original glob never looked at. The glob now covers every
`.cs` file under packages/microservices/, and the regex no longer requires a specific variable
name before the indexer — a third differently-named reader introduced later must still be caught
without this script needing an update, which is the whole point of a reachability-style check.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
GENERATE_SECRETS_SH = REPO_ROOT / "kubernetes/secrets/generate-secrets.sh"
PROVISIONING_GLOB = "packages/microservices/**/*.cs"

FALLBACK_RE = re.compile(r'\["ProvisioningAppRole"\]\s*\?\?\s*"([a-z_][a-z0-9_]*)"')
APP_DB_USER_RE = re.compile(r'^\s*APP_DB_USER\s*=\s*"?([a-z_][a-z0-9_]*)"?\s*$', re.MULTILINE)


def canonical_app_role(text: str) -> str | None:
    """The role generate-secrets.sh creates and puts in the Kubernetes Secret."""
    match = APP_DB_USER_RE.search(text)
    return match.group(1) if match else None


def find_fallbacks(root: Path) -> dict[Path, list[str]]:
    """Every ProvisioningAppRole fallback, keyed by the file it appears in."""
    found: dict[Path, list[str]] = {}
    for path in sorted(root.glob(PROVISIONING_GLOB)):
        if "/obj/" in str(path) or "/bin/" in str(path):
            continue
        matches = FALLBACK_RE.findall(path.read_text(encoding="utf-8-sig"))
        if matches:
            found[path] = matches
    return found


def check(root: Path) -> list[str]:
    problems: list[str] = []

    secrets_path = root / GENERATE_SECRETS_SH.relative_to(REPO_ROOT)
    if not secrets_path.exists():
        return [f"{secrets_path} not found — this checker needs updating to match a refactor."]

    canonical = canonical_app_role(secrets_path.read_text(encoding="utf-8"))
    if canonical is None:
        return [f"Could not find APP_DB_USER in {secrets_path}. "
                "It is the source of truth for the runtime role name."]

    fallbacks = find_fallbacks(root)
    if not fallbacks:
        return ["No ProvisioningAppRole fallbacks found at all — this checker needs "
                "updating to match a refactor."]

    for path, roles in fallbacks.items():
        rel = path.relative_to(root)
        for role in roles:
            if role != canonical:
                problems.append(
                    f"{rel}: ProvisioningAppRole falls back to '{role}', but "
                    f"generate-secrets.sh creates '{canonical}'. Granting to a role that does "
                    f"not exist makes tenant provisioning fail for this service. If the role "
                    f"is genuinely being renamed, that is a privilege migration (grant the new "
                    f"role on every existing tenant_* schema first) — see #196 and #197."
                )
    return problems


def main() -> int:
    problems = check(REPO_ROOT)
    if problems:
        print(f"::error::Found {len(problems)} provisioning app-role problem(s):")
        for p in problems:
            print(f"::error::{p}")
        return 1
    print("All ProvisioningAppRole fallbacks match generate-secrets.sh's APP_DB_USER.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
