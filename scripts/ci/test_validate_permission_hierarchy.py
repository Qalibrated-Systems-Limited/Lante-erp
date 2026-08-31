"""
Tests for validate_permission_hierarchy.py — builds real fixture files under a temp directory for
the parsers, and calls compare() directly for the comparison logic. Run with:
python3 -m unittest test_validate_permission_hierarchy -v

Three of these exist because the checker silently failed them when it was first written, and only
running it that way showed it. They are the failure modes, not the happy path.
"""
from __future__ import annotations

import io
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

from validate_permission_hierarchy import (
    unclosed_accepted_sets,
    compare,
    compare_backends,
    parse_enforced_permissions,
    parse_frontend,
    parse_handler,
    parse_seeded_permissions,
    _show_backend_disagreements,
)

FRONTEND_JS = """
export const PERMISSIONS = ['a']

const HIERARCHY = {
  'tickets.read.all': ['tickets.read.dept', 'tickets.read.own'],
  'fleet.read': ['fleet.dispatch.request'],
}
"""

HANDLER_CS = """
public class PermissionAuthorizationHandler
{
    private static readonly Dictionary<string, string[]> _hierarchy = new()
    {
        ["tickets.read.all"] = ["tickets.read.dept", "tickets.read.own"],
        ["fleet.read"] = ["fleet.dispatch.request"],
    };
}
"""


class ParserTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _write(self, name, content):
        path = self.root / name
        path.write_text(content, encoding="utf-8")
        return str(path)

    def test_parses_the_frontend_hierarchy(self):
        parsed = parse_frontend(self._write("permissions.js", FRONTEND_JS))
        self.assertEqual(parsed["tickets.read.all"], {"tickets.read.dept", "tickets.read.own"})

    def test_parses_a_backend_handler(self):
        parsed = parse_handler(self._write("PermissionAuthorizationHandler.cs", HANDLER_CS))
        self.assertEqual(parsed["fleet.read"], {"fleet.dispatch.request"})

    def test_a_renamed_const_is_a_hard_failure_not_a_pass(self):
        path = self._write("permissions.js", FRONTEND_JS.replace("const HIERARCHY", "const HIERARCHY_RENAMED"))
        # The original guard was `"const HIERARCHY" not in src`, a SUBSTRING test that a rename to
        # HIERARCHY_RENAMED sails straight past — so the check meant to catch a stale assumption
        # did not. Found by renaming it and watching the script exit 0.
        with self.assertRaises(SystemExit) as ctx:
            parse_frontend(path)
        self.assertEqual(ctx.exception.code, 2)

    def test_an_empty_hierarchy_is_a_hard_failure(self):
        path = self._write("permissions.js", "const HIERARCHY = {}\n")
        # A checker that parses nothing and reports nothing wrong is the failure #198 exists for.
        with self.assertRaises(SystemExit) as ctx:
            parse_frontend(path)
        self.assertEqual(ctx.exception.code, 2)


class CompareTests(unittest.TestCase):
    OWNERS = {"tickets.": "ticketing", "fleet.": "fleet-service"}

    def test_agreement_is_clean(self):
        fe = {"tickets.read.all": {"tickets.read.own"}}
        be = {"ticketing": {"tickets.read.all": {"tickets.read.own"}}}
        new, baselined, unknown, stale = compare(fe, be, self.OWNERS, {}, {})
        self.assertEqual((new, baselined, unknown, stale), ([], [], [], []))

    def test_disagreement_is_reported_against_the_owning_service(self):
        fe = {"tickets.read.all": {"tickets.read.own", "tickets.delete"}}
        be = {"ticketing": {"tickets.read.all": {"tickets.read.own"}},
              "crm":       {"tickets.read.all": {"tickets.read.own", "tickets.delete"}}}
        new, _, _, _ = compare(fe, be, self.OWNERS, {}, {})
        self.assertEqual(len(new), 1)
        key, svc, owned, ui_allows, ui_hides = new[0]
        # crm also defines the key and agrees, but it is not the owner — comparing against all 13
        # copies just re-reports #204 on every run.
        self.assertEqual((key, svc, owned), ("tickets.read.all", "ticketing", True))
        self.assertEqual(ui_allows, ["tickets.delete"])
        self.assertEqual(ui_hides, [])

    def test_an_unowned_prefix_is_reported_once_not_once_per_service(self):
        fe = {"finance.read": {"finance.reports"}}
        be = {svc: {"finance.read": set()} for svc in ("crm", "hr", "operations", "procurement")}
        new, _, _, _ = compare(fe, be, {}, {}, {})
        # Four services disagree about one key. That is ONE problem, and reporting it four times
        # inflated the count from 12 to 15 in the first run against the real tree.
        self.assertEqual(len(new), 1)
        self.assertEqual(new[0][1], "crm, hr, operations, procurement")

    def test_a_key_no_backend_defines_is_informational_not_a_failure(self):
        fe = {"settings.manage": {"settings.read"}}
        be = {"ticketing": {"tickets.read.all": set()}}
        new, _, unknown, _ = compare(fe, be, self.OWNERS, {}, {})
        self.assertEqual(new, [])
        self.assertEqual(unknown, ["settings.manage"])

    def test_baselined_drift_does_not_fail_the_build(self):
        fe = {"fleet.read": {"fleet.dispatch.request"}}
        be = {"fleet-service": {"fleet.read": set()}}
        new, baselined, _, stale = compare(fe, be, self.OWNERS, {"fleet.read": "#271"}, {})
        self.assertEqual(new, [])
        self.assertEqual(len(baselined), 1)
        self.assertEqual(stale, [])

    def test_a_baselined_key_that_stopped_drifting_is_an_error(self):
        fe = {"fleet.read": {"fleet.dispatch.request"}}
        be = {"fleet-service": {"fleet.read": {"fleet.dispatch.request"}}}
        _, _, _, stale = compare(fe, be, self.OWNERS, {"fleet.read": "#271"}, {})
        # Without this the baseline becomes permanent and quietly starts hiding a different problem
        # under the same key. It can only shrink.
        self.assertEqual(stale, ["fleet.read"])

    def test_a_baselined_key_the_frontend_no_longer_defines_is_also_stale(self):
        fe = {}
        be = {"fleet-service": {"fleet.read": set()}}
        _, _, _, stale = compare(fe, be, self.OWNERS, {"fleet.read": "#271"}, {})
        # This is the case the first implementation missed: the staleness check lived inside the
        # loop over frontend keys, so an entry for a key that no longer exists was never examined.
        self.assertEqual(stale, ["fleet.read"])

    def test_an_expected_divergence_is_allowed_and_not_baselined(self):
        fe = {"fleet.read": {"fleet.dispatch.request"}}
        be = {"fleet-service": {"fleet.read": set()}}
        new, baselined, _, _ = compare(fe, be, self.OWNERS, {}, {"fleet.read": "deliberate"})
        self.assertEqual((new, baselined), ([], []))


class CompareBackendsTests(unittest.TestCase):
    def test_agreement_is_clean(self):
        be = {"crm": {"finance.read": {"finance.reports"}},
              "hr":  {"finance.read": {"finance.reports"}}}
        new, baselined = compare_backends(be, {})
        self.assertEqual((new, baselined), ([], []))

    def test_a_key_only_one_service_defines_is_not_a_disagreement(self):
        be = {"crm": {"finance.read": {"finance.reports"}},
              "hr":  {"hr.manager": {"hr.write"}}}
        new, baselined = compare_backends(be, {})
        self.assertEqual((new, baselined), ([], []))

    def test_a_minority_value_is_reported_against_the_minority_service(self):
        be = {"finance":     {"finance.read": {"finance.write", "finance.reports"}},
              "crm":         {"finance.read": {"finance.write", "finance.reports"}},
              "operations":  {"finance.read": {"finance.write"}}}
        new, baselined = compare_backends(be, {})
        self.assertEqual(len(new), 1)
        key, majority_value, disagreeing = new[0]
        self.assertEqual(key, "finance.read")
        self.assertEqual(majority_value, frozenset({"finance.write", "finance.reports"}))
        self.assertEqual(set(disagreeing), {"operations"})

    def test_an_expected_backend_divergence_is_baselined_not_new(self):
        be = {"crm":         {"projects.read.dept": {"crm.write"}},
              "operations":  {"projects.read.dept": set()},
              "procurement": {"projects.read.dept": set()}}
        new, baselined = compare_backends(be, {"projects.read.dept": "per-service scoping"})
        self.assertEqual(new, [])
        self.assertEqual(len(baselined), 1)

    def test_show_backend_disagreements_names_missing_and_extra(self):
        rows = [("finance.read", frozenset({"a", "b"}), {"operations": {"a"}, "crm": {"a", "b", "c"}})]
        buf = io.StringIO()
        with redirect_stdout(buf):
            _show_backend_disagreements(rows)
        out = buf.getvalue()
        self.assertIn("operations is missing : b", out)
        self.assertIn("crm adds extra : c", out)


class SeededVsEnforcedTests(unittest.TestCase):
    """
    The mode #282 called for: a permission that is enforced but never seeded is never a row, never
    attaches to a role and never reaches a JWT, so the check reading it can never pass. Comparing the
    hierarchy copies to each other cannot find this — both copies agreed, and both described
    permissions nobody could hold (#293).
    """

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _write(self, name, content):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
        return str(path)

    def test_parses_seeded_permission_names(self):
        path = self._write("DatabaseSeeder.cs", """
            var permissions = new[]
            {
                (PermOpsReadOwn,  "operations.read.own",  "View own assignments"),
                (PermOpsReadDept, "operations.read.dept", "View the department"),
            };
        """)
        self.assertEqual(parse_seeded_permissions(path),
                         {"operations.read.own", "operations.read.dept"})

    def test_an_empty_seeder_parse_is_a_hard_failure(self):
        path = self._write("DatabaseSeeder.cs", "// nothing here\n")
        # Parsing zero seeded permissions would make every enforced permission look unseeded, which
        # would flood CI and get the whole check switched off.
        with self.assertRaises(SystemExit) as ctx:
            parse_seeded_permissions(path)
        self.assertEqual(ctx.exception.code, 2)

    def test_parses_enforced_permissions_from_both_forms(self):
        self._write("microservices/hr/Controllers/X.cs", """
            [Authorize(Policy = "Permission:hr.read.own")]
            public IActionResult A() => Ok();
            private bool Can => User.HasClaim("permission", "hr.read.all");
            [Authorize(Policy = "AllowAll")]
            public IActionResult B() => Ok();
        """)
        found = parse_enforced_permissions(str(self.root / "microservices/**/Controllers/*.cs"))
        # The service prefix is stripped because the seeder stores the bare name, and "AllowAll" is
        # excluded because a policy name without a dot is not a permission.
        self.assertEqual(set(found), {"hr.read.own", "hr.read.all"})
        self.assertEqual(found["hr.read.own"], {"hr"})


class TransitiveClosureTests(unittest.TestCase):
    """
    The check that found twelve real gaps, none of which the frontend-vs-owner comparison could
    reach — the frontend agreed with the closure and the backend contradicted itself.
    """

    def test_a_closed_map_reports_nothing(self):
        handlers = {"ops": {
            "ops.read":  {"ops.read", "ops.write", "ops.delete", "system.admin"},
            "ops.write": {"ops.write", "ops.delete", "system.admin"},
        }}
        self.assertEqual(unclosed_accepted_sets(handlers), [])

    def test_the_real_shape_of_the_twelve_gaps_is_caught(self):
        # ops.read accepts ops.write; ops.write accepts ops.delete; ops.read does not accept
        # ops.delete. A user holding only ops.delete could destroy a record they could not open.
        handlers = {"ops": {
            "ops.read":  {"ops.read", "ops.write", "system.admin"},
            "ops.write": {"ops.write", "ops.delete", "system.admin"},
        }}
        self.assertEqual(unclosed_accepted_sets(handlers),
                         [("ops", "ops.read", ["ops.delete"])])

    def test_closure_follows_more_than_one_hop(self):
        # read -> write -> delete -> purge. A single-hop check would report only ops.delete and
        # call the map fixed once that was added, leaving ops.purge behind.
        handlers = {"ops": {
            "ops.read":   {"ops.read", "ops.write"},
            "ops.write":  {"ops.write", "ops.delete"},
            "ops.delete": {"ops.delete", "ops.purge"},
        }}
        self.assertEqual(unclosed_accepted_sets(handlers),
                         [("ops", "ops.read", ["ops.delete", "ops.purge"]),
                          ("ops", "ops.write", ["ops.purge"])])

    def test_a_cycle_terminates(self):
        # Nothing forbids two permissions accepting each other, and a naive walk would not halt.
        handlers = {"ops": {
            "a": {"a", "b"},
            "b": {"b", "a"},
        }}
        self.assertEqual(unclosed_accepted_sets(handlers), [])

    def test_a_permission_that_is_not_itself_a_key_is_not_followed(self):
        # ops.read accepts crm.write, but this service's map does not define crm.write, so there is
        # no edge to follow and nothing to add. Assuming otherwise is exactly the over-reach that
        # put crm.delete into the frontend by analogy and had to be reverted.
        handlers = {"ops": {"ops.read": {"ops.read", "crm.write"}}}
        self.assertEqual(unclosed_accepted_sets(handlers), [])

    def test_each_service_is_closed_independently(self):
        # Two copies of the same key, one closed and one not. Reporting per (service, key) is what
        # makes the fix locatable.
        handlers = {
            "good": {"x.read": {"x.read", "x.write", "x.delete"}, "x.write": {"x.write", "x.delete"}},
            "bad":  {"x.read": {"x.read", "x.write"},             "x.write": {"x.write", "x.delete"}},
        }
        self.assertEqual(unclosed_accepted_sets(handlers),
                         [("bad", "x.read", ["x.delete"])])


if __name__ == "__main__":
    unittest.main()
