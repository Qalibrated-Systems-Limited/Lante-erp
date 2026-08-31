#!/usr/bin/env python3
"""
Fail when a money-bearing service's DbContext has an unconstrained `decimal` column with no
precision/scale declared anywhere in its model-building code.

Postgres `numeric` with no precision is arbitrary precision — it stores whatever arrives,
including a value with ten decimal places. #379 was exactly this: a crm quotation line total
(`Quantity * UnitPrice`) went unrounded and stored `27.475` on a customer-facing document. The
same bug in finance would have been *rejected by the database*, because finance's `decimal`
columns already declare `numeric(18,2)`; in crm it stored silently. See #383.

This does not try to tell a money column from a quantity/percentage/rate column on its own — that
needs real judgment (a quantity can legitimately be fractional, a rate may deliberately need more
than 2dp, e.g. an exchange rate). It only checks that EVERY decimal property in a money-bearing
service's model ends up with an explicit column type by one of the patterns already in use here:

  1. A per-property Fluent API call: `.Property(x => x.Foo).HasColumnType(...)` or `.HasPrecision(...)`
  2. A `nameof(...)` loop over an array of property names calling `.HasPrecision(...)`/`.HasColumnType(...)`
  3. A blanket sweep over `GetEntityTypes().SelectMany(t => t.GetProperties()).Where(decimal)` that
     calls `.SetColumnType(...)` — the "money-column precision backstop (#383)" pattern, which
     defaults every decimal to a sane precision and lets any of the above override it for specific
     properties that need something else.

A service passes if its DbContext contains pattern 3, or if grepping the entity files for
`public decimal` properties turns up nothing pattern 1/2 doesn't already reference by name.

Deliberately regex-based, not a C# parser — matches the style of every other validator here.

Usage:  python3 scripts/ci/validate_money_column_precision.py
Exit:   0 clean, 1 an un-baselined money-bearing service has a decimal property with no precision
        anywhere, 2 could not parse.
"""
import glob
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Money-bearing services (have at least one `decimal` entity property). licensing and hse have
# none at all, so they're not here on purpose, not because they were skipped.
MONEY_SERVICES = {
    "finance": "packages/microservices/finance",
    "stores": "packages/microservices/stores",
    "hr": "packages/microservices/hr",
    "operations": "packages/microservices/operations",
    "crm": "packages/microservices/crm",
    "procurement": "packages/microservices/procurement",
    "fleet-service": "packages/microservices/fleet-service",
    "subcontracts": "packages/microservices/subcontracts",
    "reporting": "packages/microservices/reporting",
    "compliance": "packages/microservices/compliance",
    "ticketing": "packages/microservices/ticketing",
    "user-service": "packages/microservices/masterdata/user-service",
}

# service -> reason a gap is accepted rather than fixed here. The baseline can only shrink: a
# service in here that turns out to be fully covered must have its entry removed, not left stale.
KNOWN_GAPS = {}

# The decimal-precision sweep specifically (finance's original version and #383's copies word
# their comments differently, so this matches the actual code shape, not a comment string): a
# loop over every entity's properties, filtered to decimal/decimal?, calling SetColumnType. Not
# just any SetColumnType call — a service can legitimately call it for unrelated reasons (e.g.
# timestamptz/bool column fix-ups) without that meaning its decimal columns are covered.
SWEEP_MARKER_RE = re.compile(
    r"GetEntityTypes\(\)\s*\.\s*SelectMany\([^)]*GetProperties[^}]*?typeof\(decimal[^}]*?SetColumnType\(",
    re.DOTALL,
)
PER_PROPERTY_RE = re.compile(r"\.Property\(\s*\w+\s*=>\s*\w+\.(\w+)\s*\)\s*\.Has(?:ColumnType|Precision)\(")
NAMEOF_LOOP_RE = re.compile(r"nameof\((?:\w+\.)?(\w+)\)")
DECIMAL_PROP_RE = re.compile(r"public\s+decimal\??\s+(\w+)\s*\{")
COMPUTED_PROP_RE = re.compile(r"public\s+decimal\??\s+\w+\s*=>")


def die(msg):
    print(f"::error::{msg}")
    sys.exit(2)


def has_sweep(dbcontext_src):
    return bool(SWEEP_MARKER_RE.search(dbcontext_src))


def named_coverage(dbcontext_src):
    names = set(PER_PROPERTY_RE.findall(dbcontext_src))
    names |= set(NAMEOF_LOOP_RE.findall(dbcontext_src))
    return names


def all_decimal_props(service_dir):
    props = set()
    for path in glob.glob(os.path.join(REPO, service_dir, "**", "*.Core", "Entities", "*.cs"), recursive=True):
        with open(path, encoding="utf-8") as f:
            src = f.read()
        computed = set(COMPUTED_PROP_RE.findall(src))
        for m in DECIMAL_PROP_RE.finditer(src):
            name = m.group(1)
            if name not in computed:
                props.add(name)
    return props


def find_dbcontext_sources(service_dir):
    sources = []
    for path in glob.glob(os.path.join(REPO, service_dir, "**", "Data", "*DbContext.cs"), recursive=True):
        if "Tenant" in os.path.basename(path) and os.path.basename(path) != "TenantDbContext.cs":
            # Tenant<Service>DbContext : <Service>DbContext inherits OnModelCreating - skip, its
            # base class's source (matched separately) already covers it. TenantDbContext.cs in
            # user-service is its own standalone class, not a subclass, so it's still checked.
            continue
        with open(path, encoding="utf-8") as f:
            sources.append(f.read())
    # Some services (stores) configure entities via separate IEntityTypeConfiguration<T> classes
    # applied through ApplyConfigurationsFromAssembly rather than inline in OnModelCreating —
    # those need scanning too, or every property they cover reads as a false-positive gap.
    for path in glob.glob(os.path.join(REPO, service_dir, "**", "Data", "Configurations", "*.cs"), recursive=True):
        with open(path, encoding="utf-8") as f:
            sources.append(f.read())
    return sources


def main():
    failures = []
    stale = []

    for service, gap_reason in KNOWN_GAPS.items():
        if service not in MONEY_SERVICES:
            die(f"KNOWN_GAPS has '{service}' which isn't in MONEY_SERVICES — typo, or it should be removed")

    for service, service_dir in sorted(MONEY_SERVICES.items()):
        sources = find_dbcontext_sources(service_dir)
        if not sources:
            die(f"{service}: could not find a DbContext under {service_dir} — glob pattern may be stale")

        covered = has_sweep("\n".join(sources))
        named = set()
        for src in sources:
            named |= named_coverage(src)

        all_props = all_decimal_props(service_dir)
        uncovered = set() if covered else (all_props - named)

        if uncovered:
            if service in KNOWN_GAPS:
                continue
            failures.append(f"{service}: decimal properties with no precision anywhere: {sorted(uncovered)}")
        elif service in KNOWN_GAPS:
            stale.append(service)

    if stale:
        die(
            "KNOWN_GAPS has a stale entry that's actually fully covered now — remove it, don't "
            f"leave it baselined: {sorted(stale)}"
        )

    if failures:
        for f in failures:
            print(f"::error::{f}")
        sys.exit(1)

    print(f"OK: {len(MONEY_SERVICES)} money-bearing services all have decimal-column precision coverage.")
    sys.exit(0)


if __name__ == "__main__":
    main()
