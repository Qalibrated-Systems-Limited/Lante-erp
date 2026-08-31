#!/usr/bin/env python3
"""
Fail when a gateway swagger-*-service YARP route's path rewrite disagrees with what the
backend service actually serves.

Real bug found by hand while working #225: six routes in yarp.json (operations, finance, crm,
hr, procurement, store) rewrote `/swagger/<x>/...` down to `/...` (PathPattern "/{**catch-all}"),
but every one of those services' Program.cs sets `RoutePrefix = "swagger"` in its non-Development
branch (finance uses Swashbuckle's own default, which is also "swagger") — meaning the deployed
service actually serves Swagger UI at `/swagger/...`, not at its root. Live-curled through the
gateway to confirm: all six 404'd. The correct rewrite, `/swagger/{**catch-all}`, is what every
OTHER swagger route in this file already uses.

`fleet-service` is the one legitimate exception: its Program.cs sets RoutePrefix = string.Empty
unconditionally (no Development/else split), so its root-rewriting PathPattern is correct, not a
bug — confirmed live (200, not 404). That is why this check derives the expected value per
service from that service's own Program.cs rather than assuming one fixed answer for every route.

Deliberately regex-based, not a JSON/C# parser — matches the style of every other validator here.

Usage:  python3 scripts/ci/validate_gateway_swagger_routes.py
Exit:   0 clean, 1 a route's rewrite disagrees with its service's actual RoutePrefix,
        2 could not parse (treated as failure).
"""
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
YARP_JSON = os.path.join(REPO, "packages/LanteGateway/src/LanteGateway/yarp.json")

# swagger-<x>-service route key -> that service's Program.cs, relative to REPO.
# Hand-maintained rather than derived from ClusterId, because chart/directory naming already
# disagrees in three places (#227 item 3) and stacking a second inconsistent-naming guess on
# top of that would make failures here harder to read, not easier.
ROUTE_TO_PROGRAM_CS = {
    "swagger-user-service": "packages/microservices/masterdata/user-service/src/UserService.Api/Program.cs",
    "swagger-ticketing-service": "packages/microservices/ticketing/src/TicketingService.Api/Program.cs",
    "swagger-project-service": "packages/microservices/operations/src/OperationsService.Api/Program.cs",
    "swagger-operation-service": "packages/microservices/operations/src/OperationsService.Api/Program.cs",
    "swagger-license-service": "packages/microservices/licensing/src/LicenseService.Api/Program.cs",
    "swagger-fleet-service": "packages/microservices/fleet-service/src/FleetService.Api/Program.cs",
    "swagger-finance-service": "packages/microservices/finance/src/FinanceService.Api/Program.cs",
    "swagger-hse-service": "packages/microservices/hse/src/HSEService.Api/Program.cs",
    "swagger-compliance-service": "packages/microservices/compliance/src/ComplianceService.Api/Program.cs",
    "swagger-subcontracts-service": "packages/microservices/subcontracts/src/SubcontractsService.Api/Program.cs",
    "swagger-reporting-service": "packages/microservices/reporting/src/ReportingService.Api/Program.cs",
    "swagger-crm-service": "packages/microservices/crm/src/CrmService.Api/Program.cs",
    "swagger-hr-service": "packages/microservices/hr/src/HrService.Api/Program.cs",
    "swagger-procurement-service": "packages/microservices/procurement/src/ProcurementService.Api/Program.cs",
    "swagger-store-service": "packages/microservices/stores/src/StoreService.Api/Program.cs",
}


def die(msg):
    print(f"::error::{msg}")
    sys.exit(2)


def swagger_routes(yarp_src):
    """key -> PathPattern, for every route whose key starts with 'swagger-'."""
    data = json.loads(yarp_src)
    routes = data["ReverseProxy"]["Routes"]
    found = {}
    for key, route in routes.items():
        if not key.startswith("swagger-"):
            continue
        transforms = route.get("Transforms", [])
        patterns = [t["PathPattern"] for t in transforms if "PathPattern" in t]
        if len(patterns) != 1:
            die(f"{key}: expected exactly one Transforms[].PathPattern, found {len(patterns)}.")
        found[key] = patterns[0]
    if not found:
        die(f"{YARP_JSON}: parsed zero swagger-* routes — format changed?")
    return found


def production_route_prefix(src, path):
    """
    The RoutePrefix Swashbuckle actually serves under when NOT in Development (i.e. what's
    deployed). Three shapes, all seen in this codebase:
      1. if (...IsDevelopment()) { RoutePrefix = "" } else { RoutePrefix = "swagger" } -> "swagger"
      2. A single unconditional `RoutePrefix = string.Empty;` (no Development branch) -> ""
      3. `app.UseSwaggerUI();` with no RoutePrefix override at all -> "swagger" (library default)
    """
    if "IsDevelopment()" in src:
        # Take the RoutePrefix assigned in whichever branch does NOT set string.Empty — the
        # empty one is always the Development branch in every instance of this pattern seen so
        # far. Assert that shape rather than assuming it, so a differently-shaped conditional
        # fails loudly instead of silently returning the wrong answer.
        prefixes = re.findall(r'RoutePrefix\s*=\s*(string\.Empty|"[^"]*")', src)
        if prefixes != ["string.Empty", '"swagger"']:
            die(f"{path}: has IsDevelopment() and RoutePrefix, but not in the expected "
                f"[empty-then-swagger] shape ({prefixes}) — the checker's assumption is stale.")
        return "swagger"

    prefixes = re.findall(r'RoutePrefix\s*=\s*(string\.Empty|"[^"]*")', src)
    if prefixes == ["string.Empty"]:
        return ""
    if prefixes:
        die(f"{path}: unrecognized single-RoutePrefix shape ({prefixes}) — the checker's "
            f"assumption is stale.")

    if "UseSwaggerUI()" in src:
        return "swagger"  # Swashbuckle's own default when RoutePrefix is never set.

    die(f"{path}: no UseSwaggerUI call found at all — has this service dropped Swagger, or "
        f"moved its Program.cs?")


def expected_pattern(prefix):
    return f"/swagger/{{**catch-all}}" if prefix == "swagger" else "/{**catch-all}"


def main():
    with open(YARP_JSON, encoding="utf-8") as fh:
        yarp_src = fh.read()
    routes = swagger_routes(yarp_src)

    errors = []
    for key, actual_pattern in sorted(routes.items()):
        program_cs = ROUTE_TO_PROGRAM_CS.get(key)
        if program_cs is None:
            errors.append(f"{key}: no entry in ROUTE_TO_PROGRAM_CS — add one so this route is "
                           f"actually checked, rather than silently skipped.")
            continue
        full_path = os.path.join(REPO, program_cs)
        if not os.path.isfile(full_path):
            die(f"{program_cs}: does not exist — {key}'s mapping in ROUTE_TO_PROGRAM_CS is stale.")
        with open(full_path, encoding="utf-8") as fh:
            prefix = production_route_prefix(fh.read(), program_cs)
        expected = expected_pattern(prefix)
        if actual_pattern != expected:
            errors.append(
                f"{key}: yarp.json rewrites to '{actual_pattern}', but {program_cs} serves "
                f"Swagger at RoutePrefix='{prefix or '(empty)'}' in production — expected "
                f"'{expected}'. A user hitting this route through the gateway gets a 404."
            )

    print(f"swagger routes checked: {len(routes)}")

    if errors:
        for e in errors:
            print(f"::error::{e}")
        print(f"\n{len(errors)} mismatch(es) found. See #225.")
        return 1

    print("\nEvery swagger route's rewrite matches its service's actual RoutePrefix.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
