#!/usr/bin/env python3
"""
Fail when the frontend permission hierarchy drifts from the backend's.

The hierarchy — which permissions imply which — is hand-copied into every service's
PermissionAuthorizationHandler.cs and again into apps/lante_frontend/src/utils/permissions.js.
Nothing keeps those copies honest, and they have drifted: see #204 (backend vs backend) and
#271 (frontend vs backend, 36 pairs measured).

This checks the frontend against ONE nominated backend copy per permission prefix — the service
that actually serves that resource — rather than against all 13, because the 13 disagree with
each other and comparing against all of them just reports that fact repeatedly.

NOTE — there is a SECOND checker for the same class: scripts/ci/validate_permission_seeder_coverage.py,
built independently within hours of this one. Both compare enforced permissions against the seeder, and
each carries its own baseline (KNOWN_UNSEEDED here, KNOWN_MISSING there). That duplication has already
cost something: #293 seeded all 22 permissions, one baseline was emptied and the other was not, and main
went red for two commits. Worth consolidating into one script — the irony of maintaining two copies of a
check whose whole subject is duplicated permission definitions is not lost on me. Tracked in #204's
neighbourhood; whoever does it should keep the seeder-coverage script's parser, which handles more
reference forms than this one.

Second mode: every permission a service ENFORCES must exist in the seeder. A permission that is
never seeded is never a row, never attaches to a role and never reaches a JWT, so the check that
reads it is permanently false — `operations.read.all` and `operations.read.dept` were enforced in
four controllers and seeded nowhere (#282). That is not caught by comparing hierarchies to each
other: both copies agreed, and both described a permission nobody could hold.

Scope, stated because silence about it would be misleading: this compares the SHARED keys only.
A frontend key the owning backend never defines is skipped, and backend keys the frontend has
never heard of are not walked at all — both are counted and printed, but neither fails the build,
because "the frontend does not model this permission" is usually a gap rather than a contradiction.
What fails is the two copies claiming DIFFERENT things about the same key.

Deliberate divergences go in EXPECTED_DIVERGENCES with a reason. Pre-existing drift goes in
KNOWN_DRIFT, which is a different thing and is treated differently — see below. Anything else
fails the build.

Usage:  python3 scripts/ci/validate_permission_hierarchy.py
Exit:   0 clean, 1 drift found, 2 could not parse (treated as failure — a checker that
        silently finds nothing is the bug this exists to prevent).
"""
import glob
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FRONTEND = os.path.join(REPO, "apps/lante_frontend/src/utils/permissions.js")
HANDLERS = os.path.join(REPO, "packages/microservices/**/PermissionAuthorizationHandler.cs")
SEEDER = os.path.join(REPO, "packages/microservices/masterdata/user-service/src/"
                            "UserService.Infrastructure/Data/DatabaseSeeder.cs")
CONTROLLERS = os.path.join(REPO, "packages/microservices/**/Controllers/*.cs")

# Permissions that are deliberately enforced without being seeded.
EXPECTED_UNSEEDED = {
    "system.admin": "cross-cutting bypass, granted via role composition rather than as its own row",
}

# Enforced-but-unseeded permissions that already existed when this mode landed, so NEW ones fail the
# build without a 22-permission fix having to land first. Tracked in #293.
#
# These are not cosmetic. hr.*, crm.* and procurement.* appear nowhere in the global permission
# catalog and are granted to no role, so every endpoint gating on them is reachable only by a holder
# of system.admin — which is role-admin and role-md and nobody else. HR alone has 244 policy sites.
# The failure is fail-CLOSED (too restrictive, not too permissive), which is why it went unnoticed:
# whoever tested it was an admin, and for an admin it works.
#
# Checked in both directions, like KNOWN_DRIFT: an entry that becomes seeded FAILS, so this can only
# shrink.
KNOWN_UNSEEDED = {
    # Emptied by #293, which seeded all 22. It started as hr.* (9), crm.* (6), procurement.* (4),
    # calibration.* (2) and reports.schedule — every one of them enforced by its service and present in
    # no catalog, leaving HR, CRM and procurement reachable only by system.admin. The both-directions
    # check is what retired them: it refused to pass while the baseline described a problem that had
    # been fixed. Fourth time that has happened since the mechanism landed.
}

# Which service owns which permission prefix — the copy the frontend must match.
# A prefix with no owner listed is compared against every service that defines it, and any
# disagreement among those is reported as backend-vs-backend drift (#204) rather than ignored.
OWNERS = {
    # finance had no PermissionAuthorizationHandler at all until #277 — it enforced no permissions,
    # so there was nothing to compare the frontend against. Now there is, and it was written to match
    # permissions.js deliberately, so this entry is what keeps the two from drifting apart again.
    "finance.":      "finance",
    "licensing.":    "licensing",
    "tickets.":      "ticketing",
    "fleet.":        "fleet-service",
    "operations.":   "operations",
    "projects.":     "operations",
    "hr.":           "hr",
    "crm.":          "crm",
    "procurement.":  "procurement",
    "stores.":       "stores",
    "subcontracts.": "subcontracts",
    "hse.":          "hse",
    "compliance.":   "compliance",
    "users.":        "user-service",
    "roles.":        "user-service",
    "permissions.":  "user-service",
}

# key -> reason. Use ONLY for divergence that is correct and intentional — the two copies are meant
# to disagree. Never use it to silence something merely unfixed; that is what KNOWN_DRIFT is for.
EXPECTED_DIVERGENCES = {}

# key -> reason, for the BACKEND-VS-BACKEND check (compare_backends), a distinct mode from the
# frontend-vs-owner check above. #204 is specifically about services disagreeing with each other;
# #271 measured the frontend-vs-backend half but never the backend-vs-backend half, so a
# disagreement on a key the frontend never references (most of hr.*/crm.*/procurement.*, and
# fleet.read before it was fixed) was invisible to every check that existed until this one.
#
# Verified before writing this: every backend-vs-backend disagreement found (7, the day this
# landed) was between the ONE service that actually enforces the key via a real
# [Authorize]/HasClaim check and N other services whose copy is never consulted by any local
# code path — confirmed by grepping each non-owning service for the key outside its
# PermissionAuthorizationHandler.cs. Five of the seven were exactly that: dead copies that had
# gone stale after the owner's copy gained an entry (finance.reports on finance.read,
# fleet.dispatch.request on fleet.read, platform.licensing.manage instead of system.admin on all
# three licensing.* keys) — fixed by aligning the dead copies to the live owner, zero functional
# risk since nothing reads them. The two below are different: every service that defines them
# adds ITS OWN plausible local write/approve permissions, which reads as intentional per-service
# scoping rather than accidental drift, so they are baselined here rather than unioned or forced
# to agree — per the issue's own caution against blindly unioning.
EXPECTED_BACKEND_DIVERGENCES = {
    "projects.read.dept": "crm/operations/procurement each add their own domain write/approve "
                           "permission (crm.write, operations.write, procurement.write) as also "
                           "granting project visibility from that service's own perspective — a "
                           "per-service scoping choice, not drift",
    "projects.read.own": "same reasoning as projects.read.dept",
}

# Drift that already existed when this check landed, so the check can be enforced on NEW drift
# without a 12-permission fix having to land first. Tracked in #271.
#
# Started at 12, now 5. The three licensing.* entries came off within minutes of being written (#274
# landed while this branch was in review), finance.read came off when #277 gave finance a
# permission handler at all, and fleet.read plus the two subcontracts.* entries came off when the
# UI was given the three permissions their owning services already accepted. Every time, the
# staleness check below is what noticed — the baseline was describing problems that no longer
# existed. That is why it is verified in both directions rather than only suppressing.
#
# 9 -> 6: fleet.read, subcontracts.read and subcontracts.write came off with the fix that added the
# three permissions the owning services already accept but the UI hid. The staleness check demanded
# it, exactly as it did for licensing.
#
# This list is checked in BOTH directions: an entry that is no longer drifting is an ERROR, not a
# pass. Otherwise the baseline silently becomes permanent, which is how a suppression list turns
# into a second source of truth nobody reads. It can only shrink.
# 6 -> 5 -> 4: operations.read.own came off when the transitive-closure check below was added.
# It was drifting only because operations' own map contradicted itself — it accepted
# operations.write for that key, and operations.write accepts operations.delete, but the key did
# not. The UI had the closure right all along. Closing the gap in the backend removed the drift
# without touching the frontend entry, which is the tell that the frontend was never the wrong one.
KNOWN_DRIFT = {}
# EMPTY, and it should stay that way — #271 is closed out.
#
# 9 -> 6 -> 5 -> 4 -> 0. The last four came off together once the projects.* and tickets.* read
# requirements accepted the write-side permissions for their own resource. Worth recording WHY the
# frontend was never edited to reach zero: every one of these was the backend refusing something
# the UI correctly allowed, so each fix was an addition to a handler, not a removal from
# permissions.js. A drift list is not a list of frontend mistakes, and reading it as one is how it
# grew to nine in the first place.
#
# Checked in BOTH directions: an entry that is no longer drifting is an ERROR, not a pass. That is
# what forced every one of these shrinks — each time, the checker refused to go green until the
# baseline stopped describing a problem that no longer existed.


def parse_frontend(path):
    with open(path, encoding="utf-8") as fh:
        src = fh.read()
    # Anchored, not a substring test. `"const HIERARCHY" in src` also matches HIERARCHY_RENAMED,
    # so the guard meant to catch a stale assumption sailed straight past a rename — verified by
    # renaming it and watching this exit 0.
    if not re.search(r"const\s+HIERARCHY\s*=", src):
        die(f"{path}: no `const HIERARCHY =` — the checker's assumption about this file is stale.")
    body = src.split("const HIERARCHY", 1)[1]
    out = {
        m.group(1): set(re.findall(r"'([^']+)'", m.group(2)))
        for m in re.finditer(r"'([a-zA-Z0-9_.]+)'\s*:\s*\[([^\]]*)\]", body)
    }
    if not out:
        die(f"{path}: HIERARCHY parsed to zero entries — format changed?")
    return out


def parse_handler(path):
    with open(path, encoding="utf-8") as fh:
        src = fh.read()
    if "_hierarchy" not in src:
        return {}
    body = src.split("_hierarchy", 1)[1]
    # Matches C# collection-expression form:  ["key"] = ["a", "b"],
    return {
        m.group(1): set(re.findall(r'"([^"]+)"', m.group(2)))
        for m in re.finditer(r'\["([a-zA-Z0-9_.]+)"\]\s*=\s*\[([^\]]*)\]', body)
    }


def parse_seeded_permissions(path):
    """The permission NAMES the seeder creates — the second element of each (const, "name", desc) row."""
    with open(path, encoding="utf-8") as fh:
        src = fh.read()
    names = set(re.findall(r'\(\s*Perm\w+\s*,\s*"([a-z0-9_.]+)"', src))
    if not names:
        die(f"{path}: parsed zero seeded permissions — the (const, \"name\", description) shape changed?")
    return names


def parse_enforced_permissions(pattern):
    """
    Permission strings the services actually gate on: `[Authorize(Policy = "…")]` and
    `HasClaim("permission", "…")`. Policy names may carry a service prefix (`Permission:ops.read`),
    which is stripped — the seeder stores the bare name.
    """
    found = {}
    for path in sorted(glob.glob(pattern, recursive=True)):
        with open(path, encoding="utf-8") as fh:
            src = fh.read()
        hits = re.findall(r'Authorize\(Policy\s*=\s*"([^"]+)"', src)
        hits += re.findall(r'HasClaim\(\s*"permission"\s*,\s*"([^"]+)"', src)
        for h in hits:
            name = h.split(":", 1)[1] if ":" in h else h
            if "." not in name:          # policy names that are not permissions, e.g. "AllowAll"
                continue
            found.setdefault(name, set()).add(path.split("/microservices/")[1].split("/")[0])
    return found


def service_name(path):
    return path.split("/src/")[0].split("/")[-1]


def die(msg):
    print(f"::error::{msg}")
    sys.exit(2)


def compare(frontend, handlers, owners=OWNERS, known_drift=KNOWN_DRIFT,
            expected=EXPECTED_DIVERGENCES):
    """
    Returns (new_drift, baselined, unknown_to_backend, stale).

    Split out from main() so it can be tested against fixtures rather than only against the real
    tree — the same shape the other validators in this directory use. A checker whose logic can
    only be exercised end-to-end is one whose failure modes never get tested.
    """
    new_drift, baselined, unknown_to_backend, allowed = [], [], [], []
    for key in sorted(frontend):
        prefix = next((pfx for pfx in owners if key.startswith(pfx)), None)
        owner = owners.get(prefix)
        owned = bool(owner and owner in handlers)
        targets = [owner] if owned else list(handlers)

        # One finding per KEY, not per service. Without an owner the same divergence is otherwise
        # reported once for every service that happens to define the key — finance.read appeared
        # four times — which inflates the count and reads as four separate problems.
        disagreeing = {}
        defined_anywhere = False
        for svc in targets:
            backend = handlers[svc].get(key)
            if backend is None:
                continue
            defined_anywhere = True
            if backend != frontend[key]:
                disagreeing[svc] = backend

        if not defined_anywhere:
            unknown_to_backend.append(key)
            continue
        if not disagreeing:
            continue
        if key in expected:
            allowed.append(key)
            continue

        svc = ", ".join(sorted(disagreeing))
        backend = disagreeing[sorted(disagreeing)[0]]
        row = (key, svc, owned,
               sorted(frontend[key] - backend), sorted(backend - frontend[key]))
        (baselined if key in known_drift else new_drift).append(row)

    # Every baselined key must still be drifting. Computed OUTSIDE the loop over frontend keys,
    # because a baseline entry whose key the frontend no longer defines would otherwise never be
    # examined at all — the loop only walks keys that currently exist. That is how a suppression
    # list quietly outlives the problem it describes and starts hiding a different one.
    stale = sorted(set(known_drift) - {k for k, _, _, _, _ in baselined})
    return new_drift, baselined, unknown_to_backend, stale


def compare_backends(handlers, expected=EXPECTED_BACKEND_DIVERGENCES):
    """
    #204: do the 14 backend copies agree with EACH OTHER, independent of the frontend. Walks
    every key any handler defines — not just keys the frontend happens to reference, which is
    the gap that let five of these disagreements go undetected by the frontend-vs-owner check
    above for however long they had existed.

    Returns (new_disagreements, baselined). Each row is (key, majority_value, {service: value}
    for every service whose value differs from the majority) — deliberately its own shape, not
    reused from compare()'s rows, since "UI allows/hides" framing doesn't apply here: there is no
    frontend and no single "owner" concept, just N backend copies that either agree or don't.
    """
    all_keys = set()
    for h in handlers.values():
        all_keys |= set(h.keys())

    new_disagreements, baselined = [], []
    for key in sorted(all_keys):
        values = {svc: h[key] for svc, h in handlers.items() if key in h}
        counts = {}
        for v in values.values():
            counts[frozenset(v)] = counts.get(frozenset(v), 0) + 1
        if len(counts) <= 1:
            continue
        majority_value = max(counts, key=counts.get)
        disagreeing = {svc: v for svc, v in values.items() if frozenset(v) != majority_value}
        row = (key, majority_value, disagreeing)
        (baselined if key in expected else new_disagreements).append(row)

    return new_disagreements, baselined


def unclosed_accepted_sets(handlers):
    """
    Is each accepted-set transitively closed?

    These maps read "requirement -> permissions that satisfy it", so if X satisfies `key` and Y
    satisfies X, then Y satisfies `key`. That is a property of what the map MEANS, not a policy
    choice, and it can be checked rather than reviewed.

    It was violated twelve times across crm, hr, operations and procurement, always the same way:
    `<x>.write` accepted `<x>.delete`, and `<x>.read.*` accepted `<x>.write`, but `<x>.read.*` did
    not accept `<x>.delete` — so a user holding only `<x>.delete` could destroy a record they were
    not allowed to open. None of it was reachable by the frontend-vs-owner comparison, because the
    frontend agreed with the closure and the backend was the one contradicting itself.

    No baseline, deliberately. A closure gap is never a legitimate per-service choice: it says the
    map disagrees with its own other entries. Anything found here is a defect by construction, so
    there is nothing an exception list could honestly hold.

    Returns [(service, key, sorted(missing))].
    """
    gaps = []
    for svc, h in sorted(handlers.items()):
        for key, accepted in sorted(h.items()):
            closure, changed = set(accepted), True
            while changed:
                changed = False
                for granted in list(closure):
                    for implied in h.get(granted, ()):
                        if implied not in closure:
                            closure.add(implied)
                            changed = True
            missing = closure - set(accepted)
            if missing:
                gaps.append((svc, key, sorted(missing)))
    return gaps


def _show_backend_disagreements(rows):
    for key, majority_value, disagreeing in rows:
        print(f"  {key}")
        for svc, v in sorted(disagreeing.items()):
            missing = sorted(majority_value - set(v))
            extra = sorted(set(v) - majority_value)
            if missing:
                print(f"      {svc} is missing : {', '.join(missing)}")
            if extra:
                print(f"      {svc} adds extra : {', '.join(extra)}")


def _show(rows):
    for key, svc, owned, ui_allows, ui_hides in rows:
        label = f"owner: {svc}" if owned else f"vs {svc} — no owning service declared for this prefix"
        print(f"  {key}  ({label})")
        if ui_allows:
            print(f"      UI allows, API denies : {', '.join(ui_allows)}")
        if ui_hides:
            print(f"      UI hides, API allows  : {', '.join(ui_hides)}")


def main():
    frontend = parse_frontend(FRONTEND)
    handlers = {}
    for path in sorted(glob.glob(HANDLERS, recursive=True)):
        parsed = parse_handler(path)
        if parsed:
            handlers[service_name(path)] = parsed
    if not handlers:
        die("No PermissionAuthorizationHandler.cs parsed. Either they moved or the "
            "`[\"key\"] = [...]` form changed — this must not pass silently.")

    seeded = parse_seeded_permissions(SEEDER)
    enforced = parse_enforced_permissions(CONTROLLERS)
    all_unseeded = {k: v for k, v in sorted(enforced.items())
                    if k not in seeded and k not in EXPECTED_UNSEEDED}
    unseeded = {k: v for k, v in all_unseeded.items() if k not in KNOWN_UNSEEDED}
    baselined_unseeded = sorted(set(all_unseeded) & set(KNOWN_UNSEEDED))
    fixed_unseeded = sorted(set(KNOWN_UNSEEDED) - set(all_unseeded))

    for name in fixed_unseeded:
        print(f"::error::{name} is listed in KNOWN_UNSEEDED but is now seeded — delete the entry. "
              f"The baseline can only shrink.")
    if baselined_unseeded:
        print(f"{len(baselined_unseeded)} enforced-but-unseeded, tracked in #293 (not a failure)")

    print(f"frontend keys: {len(frontend)} | backend copies: {len(handlers)} | "
          f"seeded permissions: {len(seeded)} | enforced: {len(enforced)}")

    for name, services in unseeded.items():
        print(f"::error::{name} is enforced by {', '.join(sorted(services))} but is not seeded. "
              f"An unseeded permission is never a row, never attaches to a role and never reaches a "
              f"JWT, so the check that reads it can never pass. See #282.")
    new_drift, baselined, unknown_to_backend, stale = compare(frontend, handlers)
    new_backend_drift, backend_baselined = compare_backends(handlers)
    unclosed = unclosed_accepted_sets(handlers)

    for key in stale:
        print(f"::error::{key} is listed in KNOWN_DRIFT but is not drifting — either it was fixed "
              f"and the entry must be deleted, or the key no longer exists. The baseline can only shrink.")

    if unknown_to_backend:
        print(f"\n{len(unknown_to_backend)} frontend key(s) no backend defines (not a failure): "
              f"{', '.join(unknown_to_backend)}")

    if baselined:
        print(f"\n{len(baselined)} known drift, already tracked in #271 (not a failure):\n")
        _show(baselined)

    if backend_baselined:
        print(f"\n{len(backend_baselined)} expected backend-vs-backend divergence, tracked in "
              f"#204 (not a failure):\n")
        _show_backend_disagreements(backend_baselined)

    if unclosed:
        print(f"\n{len(unclosed)} accepted-set(s) not transitively closed:\n")
        for svc, key, missing in unclosed:
            print(f"  {svc}  {key}")
            print(f"      accepts a permission that implies, but does not itself accept: "
                  f"{', '.join(missing)}")
        print("\nAdd the missing permissions. If X satisfies this requirement and Y satisfies X, "
              "then Y satisfies this requirement — a map that says otherwise contradicts itself, "
              "and the usual symptom is a user who can delete a record they cannot open.")
        print("::error::A permission hierarchy is not transitively closed. See #271.")
        return 1

    if fixed_unseeded:
        print(f"\n{len(fixed_unseeded)} stale KNOWN_UNSEEDED entr(y/ies). Remove them.")
        return 1

    if unseeded:
        print(f"\n{len(unseeded)} NEW enforced permission(s) that nothing seeds: "
              f"{', '.join(sorted(unseeded))}")
        return 1

    if stale:
        print(f"\n{len(stale)} stale KNOWN_DRIFT entr(y/ies). Remove them.")
        return 1

    if new_backend_drift:
        print(f"\n{len(new_backend_drift)} backend-vs-backend disagreement(s) NOT baselined:\n")
        _show_backend_disagreements(new_backend_drift)
        print("\nReconcile deliberately — decide which copy is right rather than unioning, per "
              "#204's own caution — or add an entry to EXPECTED_BACKEND_DIVERGENCES with a "
              "reason if every value is a legitimate per-service choice.")
        print("::error::PermissionAuthorizationHandler.cs copies disagree with each other. See #204.")

    if not new_drift and not new_backend_drift:
        print("\nNo NEW permission drift.")
        return 0

    if new_drift:
        print(f"\n{len(new_drift)} permission(s) drifted and are NOT baselined:\n")
        _show(new_drift)
        print("\nFix apps/lante_frontend/src/utils/permissions.js to match the owning backend copy, or "
              "add an entry to EXPECTED_DIVERGENCES with a reason if the divergence is deliberate.")
        print("::error::Frontend permission hierarchy has drifted from the backend. See #271.")

    return 1


if __name__ == "__main__":
    sys.exit(main())
