#!/usr/bin/env python3
"""
Every image build context must carry a .dockerignore, and the .NET ones must be identical.

Why sixteen copies rather than one: .dockerignore is resolved against the BUILD CONTEXT, not the repo
root. Every build-*.yml sets `context:` to a service directory, so a single file at the repo root has no
effect whatsoever — verified by building with one there and watching obj/ arrive in the image anyway.
Sixteen copies is the cost of per-service contexts (#241); this script is what stops them drifting.

Before these existed, each build shipped its whole service directory including obj/ and bin/ — 106 MB
for ticketing, measured, against 3.39 MB after. Beyond the wasted upload, a stale artifact in bin/ can be
copied into the image ahead of the fresh compile meant to replace it.

Contexts are DISCOVERED from the workflows, never hardcoded. A hardcoded list that stops matching reality
is the failure this repo has hit repeatedly — see #198.

Usage:  python3 scripts/ci/validate_dockerignore.py
Exit:   0 clean, 1 a context is missing one or has drifted, 2 could not parse (treated as failure — a
        checker that silently finds nothing is the bug it exists to prevent).
"""
from __future__ import annotations

import glob
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
WORKFLOWS = os.path.join(REPO, ".github/workflows/build-*.yml")

# The frontend context is legitimately different: its exclusions are node_modules and dist, not obj and
# bin. Listed explicitly so "different" has to be a decision rather than an accident.
ALLOWED_DIFFERENT = {"apps/lante_frontend"}


def die(msg: str) -> None:
    print(f"::error::{msg}")
    sys.exit(2)


def discover_contexts(pattern: str) -> dict[str, list[str]]:
    """context path -> the workflows that build it."""
    found: dict[str, list[str]] = {}
    for path in sorted(glob.glob(pattern)):
        with open(path, encoding="utf-8") as fh:
            for m in re.finditer(r"^\s+context:\s*(\S+)\s*$", fh.read(), re.MULTILINE):
                ctx = m.group(1).lstrip("./").rstrip("/")
                if ctx:
                    found.setdefault(ctx, []).append(os.path.basename(path))
    if not found:
        die("No build contexts found in .github/workflows/build-*.yml. Either the workflows moved or the "
            "`context:` key changed — this must not pass silently.")
    return found


def main() -> int:
    contexts = discover_contexts(WORKFLOWS)
    print(f"build contexts discovered: {len(contexts)}")

    missing: list[str] = []
    bodies: dict[str, str] = {}
    for ctx in sorted(contexts):
        path = os.path.join(REPO, ctx, ".dockerignore")
        if not os.path.isfile(path):
            missing.append(ctx)
            continue
        with open(path, encoding="utf-8") as fh:
            bodies[ctx] = fh.read()

    for ctx in missing:
        print(f"::error::{ctx} is a build context with no .dockerignore. Its whole directory — obj/ and "
              f"bin/ included — is uploaded to the daemon and can leak stale artifacts into the image. "
              f"A .dockerignore at the repo root does NOT cover it; the file must be in the context.")

    # The .NET contexts share one body. Compared against the most common one so the error names the odd
    # file out rather than blaming whichever happened to sort first.
    shared = {c: b for c, b in bodies.items() if c not in ALLOWED_DIFFERENT}
    drifted: list[str] = []
    if shared:
        counts: dict[str, int] = {}
        for b in shared.values():
            counts[b] = counts.get(b, 0) + 1
        canonical = max(counts, key=lambda b: counts[b])
        drifted = sorted(c for c, b in shared.items() if b != canonical)
        for ctx in drifted:
            print(f"::error::{ctx}/.dockerignore has drifted from the other "
                  f"{len(shared) - len(drifted)} .NET contexts. They exist only because .dockerignore is "
                  f"per-context; they are not meant to differ. Copy one of the others, or add {ctx} to "
                  f"ALLOWED_DIFFERENT with a reason if the difference is deliberate.")

    for ctx in sorted(ALLOWED_DIFFERENT & set(bodies)):
        print(f"  allowed to differ: {ctx}")

    if missing or drifted:
        print(f"\n{len(missing)} missing, {len(drifted)} drifted.")
        return 1

    print(f"All {len(contexts)} build contexts have a .dockerignore, and the "
          f"{len(shared)} .NET ones are identical.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
