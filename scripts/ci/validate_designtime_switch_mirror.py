#!/usr/bin/env python3
"""
Fail when a service's Program.cs sets an AppContext.SetSwitch that its own EF Core design-time
factories don't mirror.

`dotnet ef` tooling resolves a DbContext through that service's IDesignTimeDbContextFactory,
which builds DbContextOptions directly and NEVER executes Program.cs's Main() — so any
AppContext.SetSwitch call at the top of Main() (e.g. "Npgsql.EnableLegacyTimestampBehavior",
which changes Npgsql's default DateTime column-type mapping) is invisible to every `dotnet ef`
command, including `migrations add` and `has-pending-model-changes`. A migration generated that
way looks correct to every local check and then fails EF 9's PendingModelChangesWarning the
moment the real app (which DOES execute the switch) tries to apply it, because the real app's
model disagrees with what got generated.

This happened for real: the EF Core 8->9 bump crash-looped crm, operations and procurement in
production (fixed in the PR this check landed in) — each has this exact switch in Program.cs,
and none of their design-time factories mirrored it. Six more services had the identical gap
and simply hadn't hit it yet, because nobody had generated a new migration for them since the
switch was added.

Deliberately regex-based, not a C# parser — matches the style of every other validator here.

Usage:  python3 scripts/ci/validate_designtime_switch_mirror.py
Exit:   0 clean, 1 a service's factories are missing a switch its Program.cs sets,
        2 could not parse (treated as failure).
"""
import glob
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROGRAM_CS_GLOB = os.path.join(REPO, "packages/microservices/**/Program.cs")

# factory path (repo-relative) -> reason. A service whose factory genuinely doesn't mirror a
# switch its Program.cs sets, accepted deliberately rather than fixed here.
#
# Currently EMPTY, and it should stay that way.
#
# finance was the last entry and #340 closed it: its DesignTimeFactories.cs now mirrors
# Npgsql.EnableLegacyTimestampBehavior, so `dotnet ef` and the running app finally compute the
# same model. Removing the entry is not bookkeeping — this checker treats a baselined gap that
# has stopped being a gap as a FAILURE, on purpose, so a baseline can only ever shrink. #340
# fixed the code without shrinking it, which is what turned main red; that is the mechanism
# working, not misfiring.
#
# Note what is NOT resolved: mirroring the switch is what surfaced finance's 234-line pending
# timestamptz diff, because its whole migration history was generated with the switch unmirrored.
# That reconciliation is #337 and is still open. This checker only asks whether tooling and
# runtime agree on the switch — it cannot see a migration history that disagrees with both, so a
# pass here must not be read as #337 being done.
KNOWN_GAPS = {}


def die(msg):
    print(f"::error::{msg}")
    sys.exit(2)


def switches_in(src):
    return set(re.findall(r'AppContext\.SetSwitch\(\s*"([^"]+)"\s*,\s*(true|false)\s*\)', src))


def service_name(program_cs_path):
    # .../packages/microservices/<service>/src/<Something>.Api/Program.cs
    parts = program_cs_path.split(os.sep)
    idx = parts.index("microservices")
    return parts[idx + 1]


def design_time_factories_for(service_dir):
    pattern = os.path.join(service_dir, "**", "*DesignTime*.cs")
    return sorted(glob.glob(pattern, recursive=True))


def main():
    program_files = sorted(glob.glob(PROGRAM_CS_GLOB, recursive=True))
    if not program_files:
        die(f"{PROGRAM_CS_GLOB}: matched zero Program.cs files — path changed?")

    errors = []
    baselined = []
    checked = 0
    for program_cs in program_files:
        with open(program_cs, encoding="utf-8") as fh:
            program_src = fh.read()
        program_switches = switches_in(program_src)
        if not program_switches:
            continue  # Nothing to mirror for this service.

        svc = service_name(program_cs)
        service_dir = os.path.join(REPO, "packages/microservices", svc)
        factories = design_time_factories_for(service_dir)
        if not factories:
            errors.append(
                f"{program_cs} sets AppContext.SetSwitch {sorted(program_switches)}, but no "
                f"*DesignTime*.cs factory was found under {service_dir} to check against — "
                f"the factory may just have a different name pattern than this checker expects."
            )
            continue

        checked += 1
        for factory_path in factories:
            with open(factory_path, encoding="utf-8") as fh:
                factory_src = fh.read()
            factory_switches = switches_in(factory_src)
            missing = program_switches - factory_switches
            if not missing:
                continue
            rel = os.path.relpath(factory_path, REPO)
            row = (rel, sorted(missing))
            if rel in KNOWN_GAPS:
                baselined.append(row)
            else:
                errors.append(
                    f"{rel}: missing AppContext.SetSwitch {missing!r}, present in "
                    f"{os.path.relpath(program_cs, REPO)} — `dotnet ef` for this context computes "
                    f"a different model than the running app does. See #227's hotfix for the "
                    f"production incident this exact gap caused."
                )

    # A baselined gap that stops being a gap is a stale entry, not a pass — otherwise the
    # baseline silently outlives the problem it describes.
    stale = sorted(set(KNOWN_GAPS) - {rel for rel, _ in baselined})

    print(f"Program.cs files with a switch to mirror: {checked}")
    if baselined:
        print(f"{len(baselined)} known gap(s), tracked deliberately (not a failure):")
        for rel, missing in baselined:
            print(f"  {rel}: {missing} — {KNOWN_GAPS[rel]}")

    for rel in stale:
        print(f"::error::{rel} is listed in KNOWN_GAPS but no longer has a mirroring gap — "
              f"remove the entry. The baseline can only shrink.")

    if stale:
        print(f"\n{len(stale)} stale KNOWN_GAPS entr(y/ies). Remove them.")
        return 1

    if errors:
        for e in errors:
            print(f"::error::{e}")
        print(f"\n{len(errors)} mirroring gap(s) found.")
        return 1

    print("\nEvery design-time factory mirrors its Program.cs's AppContext.SetSwitch calls "
          "(outside KNOWN_GAPS).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
