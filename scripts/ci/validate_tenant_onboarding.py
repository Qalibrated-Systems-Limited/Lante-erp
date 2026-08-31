#!/usr/bin/env python3
"""
Guards against the exact class of bug found and fixed on 2026-07-17: a schema-per-tenant
microservice (has its own TenantProvisioningService.cs + InternalProvisioningController.cs)
silently never gets called during tenant onboarding, because nobody remembered to also:
  (a) add its key to PlatformServices.Business in user-service, and/or
  (b) wire ProvisioningTargets__<key> into every environment's config (Helm chart, docker-compose,
      local dev appsettings).

This happened to "reporting" (missing from (a) entirely) and to "stores" (missing from the local
dev appsettings only). Both are now fixed; this script stops it from silently recurring for a
future service, in either direction:
  - a service key in PlatformServices.Business with no config in one of the three places, or
  - a service with a real InternalProvisioningController.cs that PlatformServices.Business doesn't
    know about at all.

Deliberately not hardcoded to any tenant name or count of services — reads the actual current
source of truth (PlatformServices.cs's Business array) and the actual filesystem, so it keeps
working unmodified as services are added or removed. The one hardcoded piece is the small
directory-name -> service-key map below, because that mapping has no reliable mechanical
derivation (e.g. "fleet-service" -> "fleet", "masterdata/user-service" -> "user") — add a line to
it whenever a new schema-per-tenant service is created, the same way a new build-*.yml workflow is
already required per service.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

# Directory (relative to packages/microservices/) -> PlatformServices service key.
# Update this when a new schema-per-tenant service is added.
SERVICE_KEY_BY_DIR = {
    "compliance": "compliance",
    "crm": "crm",
    "procurement": "procurement",
    "hr": "hr",
    "finance": "finance",
    "hse": "hse",
    "licensing": "licensing",
    "operations": "operations",
    "reporting": "reporting",
    "stores": "stores",
    "subcontracts": "subcontracts",
    "ticketing": "ticketing",
    "fleet-service": "fleet",
    "masterdata/user-service": "user",
}

PLATFORM_SERVICES_CS = REPO_ROOT / "packages/microservices/masterdata/user-service/src/UserService.Core/Constants/PlatformServices.cs"
HELM_DEPLOYMENT = REPO_ROOT / "kubernetes/helm-charts/user-service/templates/deployment.yaml"
DEV_APPSETTINGS = REPO_ROOT / "packages/microservices/masterdata/user-service/src/UserService.Api/appsettings.Development.json"
DOCKER_COMPOSE = REPO_ROOT / "Deployment/docker-compose.yml"


def parse_business_keys(platform_services_cs: Path = PLATFORM_SERVICES_CS) -> set[str]:
    text = platform_services_cs.read_text()
    consts = dict(re.findall(r'public const string (\w+)\s*=\s*"([a-z_]+)";', text))
    m = re.search(r"Business\s*=\s*\n?\s*new\[\]\s*\{([^}]+)\}", text)
    if not m:
        print("::error::Could not find PlatformServices.Business array — script needs updating to match a refactor.")
        sys.exit(1)
    idents = [i.strip() for i in m.group(1).split(",") if i.strip()]
    keys = set()
    for ident in idents:
        if ident not in consts:
            print(f"::error::Business array references '{ident}' with no matching `public const string {ident} = \"...\";`")
            sys.exit(1)
        keys.add(consts[ident])
    return keys


def find_services_with_provisioning_controller(
    microservices_root: Path = REPO_ROOT / "packages/microservices",
    service_key_by_dir: dict[str, str] = SERVICE_KEY_BY_DIR,
) -> dict[str, str]:
    """Returns {service_key: relative_dir} for every service directory that has its own
    InternalProvisioningController.cs — i.e. genuinely supports being provisioned."""
    found = {}
    for controller in microservices_root.glob("**/InternalProvisioningController.cs"):
        rel = controller.relative_to(microservices_root)
        # rel looks like "compliance/src/ComplianceService.Api/Controllers/InternalProvisioningController.cs"
        # or "masterdata/user-service/src/.../InternalProvisioningController.cs"
        parts = rel.parts
        for depth in (1, 2):
            candidate = "/".join(parts[:depth])
            if candidate in service_key_by_dir:
                found[service_key_by_dir[candidate]] = candidate
                break
        else:
            print(f"::error::Found {controller} but its directory isn't in SERVICE_KEY_BY_DIR in {__file__} — add it.")
            sys.exit(1)
    return found


def find_services_with_tenant_migrations(
    microservices_root: Path = REPO_ROOT / "packages/microservices",
    service_key_by_dir: dict[str, str] = SERVICE_KEY_BY_DIR,
) -> dict[str, str]:
    """Returns {service_key: relative_dir} for every service directory that has a
    Migrations/Tenant folder — i.e. was clearly built for schema-per-tenant, whether or not
    provisioning was ever actually wired up for it. This is the check that closes the gap the
    controller-based scan can't: a brand-new service can have real tenant migrations and zero
    provisioning code at all (no controller yet), which the controller scan alone would see as
    simply "nothing to check" rather than "something is missing"."""
    found = {}
    for tenant_migrations_dir in microservices_root.glob("**/Migrations/Tenant"):
        rel = tenant_migrations_dir.relative_to(microservices_root)
        parts = rel.parts
        for depth in (1, 2):
            candidate = "/".join(parts[:depth])
            if candidate in service_key_by_dir:
                found[service_key_by_dir[candidate]] = candidate
                break
        else:
            print(f"::error::Found {tenant_migrations_dir} but its directory isn't in SERVICE_KEY_BY_DIR in {__file__} — add it.")
            sys.exit(1)
    return found


def find_services_with_tenant_interceptor(
    microservices_root: Path = REPO_ROOT / "packages/microservices",
    service_key_by_dir: dict[str, str] = SERVICE_KEY_BY_DIR,
) -> dict[str, str]:
    """Returns {service_key: relative_dir} for every service directory that has its own
    TenantDbConnectionInterceptor.cs — i.e. actually routes connections to a per-request tenant
    schema at runtime. This is the check that closes the gap neither the controller scan nor the
    Migrations/Tenant scan can see: a service can have a real, live interceptor binding search_path
    from the caller's JWT/header — and thus clearly *intends* schema-per-tenant isolation — while
    having a bespoke, ad hoc startup path that never touches InternalProvisioningController.cs or a
    Migrations/Tenant folder at all (exactly how "finance" shipped: a hardcoded
    provision-one-hardcoded-schema-at-startup loop, invisible to both other checks, so any future
    tenant beyond the one it was hardcoded for would silently get no finance schema whatsoever)."""
    found = {}
    for interceptor in microservices_root.glob("**/TenantDbConnectionInterceptor.cs"):
        rel = interceptor.relative_to(microservices_root)
        parts = rel.parts
        for depth in (1, 2):
            candidate = "/".join(parts[:depth])
            if candidate in service_key_by_dir:
                found[service_key_by_dir[candidate]] = candidate
                break
        else:
            print(f"::error::Found {interceptor} but its directory isn't in SERVICE_KEY_BY_DIR in {__file__} — add it.")
            sys.exit(1)
    return found


def check_config_wiring(
    keys: set[str],
    helm_deployment: Path = HELM_DEPLOYMENT,
    dev_appsettings: Path = DEV_APPSETTINGS,
    docker_compose: Path = DOCKER_COMPOSE,
) -> list[str]:
    problems = []
    helm_text = helm_deployment.read_text()
    dev_text = dev_appsettings.read_text()
    compose_text = docker_compose.read_text()
    for key in sorted(keys):
        if f"ProvisioningTargets__{key}" not in helm_text:
            problems.append(f"'{key}' is missing 'ProvisioningTargets__{key}' in {helm_deployment.name}")
        if f'"{key}"' not in dev_text:
            problems.append(f"'{key}' is missing from ProvisioningTargets in {dev_appsettings.name}")
        if f"ProvisioningTargets__{key}=" not in compose_text:
            problems.append(f"'{key}' is missing 'ProvisioningTargets__{key}=' in {docker_compose.name}")
    return problems


def find_wiring_problems(
    platform_services_cs: Path = PLATFORM_SERVICES_CS,
    microservices_root: Path = REPO_ROOT / "packages/microservices",
    helm_deployment: Path = HELM_DEPLOYMENT,
    dev_appsettings: Path = DEV_APPSETTINGS,
    docker_compose: Path = DOCKER_COMPOSE,
    service_key_by_dir: dict[str, str] = SERVICE_KEY_BY_DIR,
) -> list[str]:
    business_keys = parse_business_keys(platform_services_cs)
    services_with_controllers = find_services_with_provisioning_controller(microservices_root, service_key_by_dir)
    services_with_tenant_migrations = find_services_with_tenant_migrations(microservices_root, service_key_by_dir)
    services_with_interceptor = find_services_with_tenant_interceptor(microservices_root, service_key_by_dir)

    problems = check_config_wiring(business_keys, helm_deployment, dev_appsettings, docker_compose)

    for key, rel_dir in services_with_controllers.items():
        if key != "user" and key not in business_keys:
            problems.append(
                f"'{rel_dir}' has a real InternalProvisioningController.cs (service key '{key}') "
                f"but PlatformServices.Business doesn't include it — it can never be provisioned "
                f"for any tenant, current or future."
            )

    for key, rel_dir in services_with_tenant_migrations.items():
        if key != "user" and key not in services_with_controllers:
            problems.append(
                f"'{rel_dir}' has tenant-schema migrations (Migrations/Tenant) but no "
                f"InternalProvisioningController.cs at all — nothing will ever create its "
                f"tenant_* schemas for any tenant, current or future."
            )

    for key, rel_dir in services_with_interceptor.items():
        if key not in services_with_controllers and key not in services_with_tenant_migrations:
            problems.append(
                f"'{rel_dir}' has a TenantDbConnectionInterceptor.cs (it routes connections to a "
                f"per-request tenant schema) but no InternalProvisioningController.cs and no "
                f"Migrations/Tenant folder — it has no real provisioning path at all, so any "
                f"tenant beyond whatever schema it may have been bootstrapped with by hand will "
                f"silently get no schema and fall through to `public`."
            )

    return problems


def main() -> int:
    problems = find_wiring_problems()

    if problems:
        print("::error::Tenant onboarding wiring is inconsistent:")
        for p in problems:
            print(f"  - {p}")
        return 1

    business_keys = parse_business_keys()
    print(f"OK — {len(business_keys)} business services, all consistently wired: {sorted(business_keys)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
