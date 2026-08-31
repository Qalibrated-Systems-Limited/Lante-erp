#!/usr/bin/env python3
"""
Fail when the gateway's yarp.json and its Helm-chart copy have diverged.

yarp.json exists twice: packages/LanteGateway/src/LanteGateway/yarp.json (what a developer
edits, and what's baked into the gateway's Docker image) and
kubernetes/helm-charts/gateway-service/yarp.json (what the chart's ConfigMap actually mounts
over /app/yarp.json at runtime — deliberately, so ops can hot-patch routes without a rebuild;
Program.cs loads it with reloadOnChange: true). The mount means the CHART copy is what's
actually live, so editing only the source copy (easy to do — it's the one next to the code)
silently never reaches production.

This happened for real, twice: #195 (calibration-certificate routes 404'd because they only
existed in the undeployed copy) and #198/#225 (found the same day this check was written — the
two files had drifted on THREE routes: the 6 swagger-route rewrites from #225, a POST method
missing on /api/v1/system-settings, and the entire /api/v1/tenants/public/{slug} route gone —
each confirmed live via a curl through the gateway from inside the cluster).

build-gateway-service.yml now auto-syncs the chart copy from the source copy on every push that
touches it, so a human only ever has to edit one file. This check is the backstop for when that
automation doesn't run (a manual edit to only one copy, a workflow change that breaks the sync
step, etc.) — the two files are asserted byte-identical, not just semantically equivalent, since
that is exactly what the sync step produces and anything else means something bypassed it.

Usage:  python3 scripts/ci/validate_yarp_config_sync.py
Exit:   0 clean, 1 the two files differ, 2 either file is missing (treated as failure).
"""
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SOURCE = os.path.join(REPO, "packages/LanteGateway/src/LanteGateway/yarp.json")
CHART_COPY = os.path.join(REPO, "kubernetes/helm-charts/gateway-service/yarp.json")


def die(msg):
    print(f"::error::{msg}")
    sys.exit(2)


def main():
    for path in (SOURCE, CHART_COPY):
        if not os.path.isfile(path):
            die(f"{path}: does not exist — has yarp.json moved?")

    with open(SOURCE, "rb") as fh:
        source_bytes = fh.read()
    with open(CHART_COPY, "rb") as fh:
        chart_bytes = fh.read()

    if source_bytes != chart_bytes:
        print(f"::error::packages/LanteGateway/src/LanteGateway/yarp.json and "
              f"kubernetes/helm-charts/gateway-service/yarp.json have diverged. The chart copy "
              f"is what's actually mounted into the running gateway (see build-gateway-service.yml "
              f"for why) — a route that exists only in the source copy will 404 or fall through to "
              f"the wrong policy in production, exactly like #195 and #198/#225. Run: "
              f"cp packages/LanteGateway/src/LanteGateway/yarp.json "
              f"kubernetes/helm-charts/gateway-service/yarp.json")
        return 1

    print("yarp.json and its Helm-chart copy are byte-identical.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
