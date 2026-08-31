"""
Tests for validate_permission_seeder_coverage.py — builds real fixture files under a temp directory
so the parsers are exercised against actual file contents, and calls compare() directly for the
baseline logic. Run with: python3 -m unittest test_validate_permission_seeder_coverage -v
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from validate_permission_seeder_coverage import compare, find_referenced_permissions, find_seeded_permissions

CONTROLLER_CS = """
namespace Whatever;
public class SomeController
{
    // A comment mentioning [Authorize(Policy = "fake.comment.only")] must not count.
    [Authorize(Policy = "Permission:operations.read.dept")]
    public IActionResult Get() => Ok();

    private bool CanReadAll => User.HasClaim("permission", "operations.read.all");

    [Authorize(Policy = "finance.write")]
    public IActionResult Post() => Ok();
}
"""

MIGRATION_CS = """
namespace Whatever.Migrations;
public class SomeMigration
{
    // Should never be scanned — under a /Migrations/ path.
    [Authorize(Policy = "migration.only.permission")]
}
"""

SEEDER_CS = """
public static class DatabaseSeeder
{
    const string PermOpsReadDept = "perm-operations-read-dept";

    private static readonly (string, string, string)[] Permissions = new[]
    {
        (PermOpsReadDept, "operations.read.dept", "View assignments in own department"),
        (PermFinanceWrite, "finance.write", "Create and update financial records"),
    };
}
"""


class ParserTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _write(self, relative, content):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
        return str(path)

    def test_finds_both_policy_and_hasclaim_forms(self):
        self._write("SomeController.cs", CONTROLLER_CS)
        found = find_referenced_permissions(str(self.root))
        self.assertEqual(found, {"operations.read.dept", "operations.read.all", "finance.write"})

    def test_a_permission_string_inside_a_comment_does_not_count(self):
        self._write("SomeController.cs", CONTROLLER_CS)
        found = find_referenced_permissions(str(self.root))
        self.assertNotIn("fake.comment.only", found)

    def test_a_migrations_directory_is_never_scanned(self):
        self._write("Migrations/SomeMigration.cs", MIGRATION_CS)
        found = find_referenced_permissions(str(self.root))
        self.assertEqual(found, set())

    def test_parses_seeded_permissions_from_the_tuple_list(self):
        path = self._write("DatabaseSeeder.cs", SEEDER_CS)
        seeded = find_seeded_permissions(path)
        self.assertEqual(seeded, {"operations.read.dept", "finance.write"})


class CompareTests(unittest.TestCase):
    def test_a_referenced_permission_not_seeded_and_not_baselined_is_a_new_gap(self):
        new_gaps, stale = compare({"operations.read.dept"}, set(), {})
        self.assertEqual(new_gaps, {"operations.read.dept"})
        self.assertEqual(stale, set())

    def test_a_baselined_gap_that_is_still_missing_is_not_reported_again(self):
        new_gaps, stale = compare({"hr.write"}, set(), {"hr.write": "#290"})
        self.assertEqual(new_gaps, set())
        self.assertEqual(stale, set())

    def test_a_baselined_entry_that_got_seeded_is_flagged_as_stale_not_silently_passed(self):
        # This is the failure mode that matters: someone fixes hr.write, forgets to remove it from
        # KNOWN_MISSING, and the baseline silently starts lying about what's still broken.
        new_gaps, stale = compare({"hr.write"}, {"hr.write"}, {"hr.write": "#290"})
        self.assertEqual(new_gaps, set())
        self.assertEqual(stale, {"hr.write"})

    def test_a_seeded_permission_that_is_also_referenced_is_fine(self):
        new_gaps, stale = compare({"finance.write"}, {"finance.write"}, {})
        self.assertEqual(new_gaps, set())
        self.assertEqual(stale, set())


if __name__ == "__main__":
    unittest.main()
