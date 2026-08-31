"""
Tests for validate_dead_entities.py — fixture files in a temp directory, matching the convention of the
other validators here. Run with:
python3 -m unittest test_validate_dead_entities -v

The false-positive tests are the important ones. This check's first pass flagged 29 entities and the real
answer was 8; shipped then, it would have reported ~70% noise into CI and been switched off inside a
week. Each test below pins one of the classes that produced that noise, so narrowing the patterns later
fails loudly instead of quietly resurrecting it.
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

import validate_dead_entities as v


class ParseTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _ctx(self, svc, body):
        p = self.root / f"packages/microservices/{svc}/src/{svc}.Infrastructure/Data/{svc}DbContext.cs"
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(body, encoding="utf-8")

    def test_parses_dbset_declarations_with_their_service(self):
        self._ctx("finance", """
            public DbSet<Invoice> Invoices => Set<Invoice>();
            public DbSet<Currency> Currencies { get; set; } = null!;
        """)
        decls = v.parse_dbsets(str(self.root / "packages/microservices/**/*DbContext.cs"))
        self.assertEqual(set(decls), {"Invoice", "Currency"})
        self.assertEqual(decls["Invoice"], {("finance", "Invoices")})

    def test_parsing_zero_dbsets_is_a_hard_failure(self):
        # A checker that finds nothing and reports success is the bug this exists to prevent. Without
        # this, renaming the DbSet<T> form would make the whole check green while covering nothing.
        with self.assertRaises(SystemExit) as ctx:
            v.parse_dbsets(str(self.root / "packages/microservices/**/*DbContext.cs"))
        self.assertEqual(ctx.exception.code, 2)


class UseDetectionTests(unittest.TestCase):
    """Each test is one false-positive class from the 29 -> 8 reduction."""

    def test_direct_construction_counts(self):
        self.assertTrue(v.is_used("Invoice", "Invoices", ["var x = new Invoice();"]))

    def test_construction_via_a_helper_into_a_generic_list_counts(self):
        # CLASS 1. PayslipLine looked dead: it is built by a Line(...) helper into new List<PayslipLine>().
        self.assertTrue(v.is_used("PayslipLine", "PayslipLines",
                                  ["var lines = new List<PayslipLine>();\nlines.Add(Line(code, name));"]))

    def test_use_via_the_dbset_property_counts(self):
        # CLASS 2. Currency and TaxCategory looked dead until _db.Currencies was searched for — the code
        # never names the TYPE at all.
        self.assertTrue(v.is_used("Currency", "Currencies",
                                  ["var c = await _db.Currencies.FirstOrDefaultAsync();"]))

    def test_use_via_a_navigation_property_counts(self):
        # CLASS 3. AccountType looked dead; it is a.AccountType everywhere, and the navigation name has
        # nothing to do with the DbSet name.
        self.assertTrue(v.is_used("AccountType", "AccountTypes", ["if (a.AccountType == expected) { }"]))

    def test_an_automapper_entry_ALONE_does_not_rescue_an_entity(self):
        # A judgement call, and worth stating because it is the one place I disagreed with my own first
        # instinct. `CreateMap<A, B>` has two type arguments, so the `<Entity>` pattern does not match it
        # — and that is deliberate rather than an oversight. A mapping profile entry means somebody
        # intended to expose the entity; it does not mean anything ever writes or reads the table.
        #
        # operations/PerformanceMetrics is exactly this: one CreateMap and no query. It is arguably WORSE
        # than having no mapping, because it is evidence of intent that outlived the feature. Widening the
        # pattern to catch multi-argument generics would have quietly removed it from the list, which is
        # the opposite of what this check is for.
        self.assertFalse(v.is_used("PerformanceMetrics", "PerformanceMetrics",
                                   ["CreateMap<PerformanceMetrics, PerformanceMetricsReadDto>();"]))

    def test_but_an_automapper_entry_PLUS_a_query_does(self):
        # The distinction is the query, not the mapping.
        self.assertTrue(v.is_used("PerformanceMetrics", "PerformanceMetrics",
                                  ["CreateMap<PerformanceMetrics, Dto>();",
                                   "var m = await _db.PerformanceMetrics.ToListAsync();"]))

    def test_a_navigation_collection_counts(self):
        self.assertTrue(v.is_used("InvoiceLine", "InvoiceLines",
                                  ["public ICollection<InvoiceLine> Lines { get; set; }"]))

    def test_a_repository_generic_counts(self):
        self.assertTrue(v.is_used("RiskEntry", "RiskEntries",
                                  ["IGenericRepository<RiskEntry> risks"]))

    def test_an_unrelated_enum_member_of_the_same_name_does_NOT_count(self):
        # The operations PendingVerification case: an enum member happens to share the entity's name, and
        # a plain grep would have called the entity alive. Nothing here constructs, queries or navigates
        # to it.
        self.assertFalse(v.is_used("PendingVerification", "PendingVerifications",
                                   ["public enum Status\n{\n    PendingVerification,   // OTP not confirmed\n}"]))

    def test_an_entity_nothing_mentions_is_dead(self):
        self.assertFalse(v.is_used("ForexRevaluationLog", "ForexRevaluationLogs",
                                   ["public class Something { }"]))


class RealRepoTests(unittest.TestCase):
    def test_the_baseline_matches_reality(self):
        decls = v.parse_dbsets(v.CONTEXTS)
        src = v.load_sources(v.SOURCES)
        dead = {(svc, ent) for ent, places in decls.items()
                for svc, prop in places if not v.is_used(ent, prop, src.get(svc, []))}
        # Both directions. A baseline entry that is no longer dead must be deleted, or the list stops
        # describing anything true and starts hiding a different problem under the same name.
        self.assertEqual(dead, set(v.KNOWN_DEAD),
                         f"new: {sorted(dead - set(v.KNOWN_DEAD))}, revived: {sorted(set(v.KNOWN_DEAD) - dead)}")

    def test_the_sweep_actually_sees_the_repo(self):
        decls = v.parse_dbsets(v.CONTEXTS)
        # Guards against a glob or path change silently reducing coverage to a handful.
        self.assertGreater(sum(len(p) for p in decls.values()), 200)


if __name__ == "__main__":
    unittest.main()
