"""
Tests for validate_soft_delete_reachability.py — fixture files in a temp directory, matching the
convention of the other validators here. Run with:
python3 -m unittest test_validate_soft_delete_reachability -v
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

import validate_soft_delete_reachability as v


class ParseTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self._orig_base = v.BASE_ENTITY_GLOB
        self._orig_src = v.SRC_GLOB
        v.BASE_ENTITY_GLOB = str(self.root / "packages/microservices/**/BaseEntity.cs")
        v.SRC_GLOB = str(self.root / "packages/microservices/**/*.cs")

    def tearDown(self):
        v.BASE_ENTITY_GLOB = self._orig_base
        v.SRC_GLOB = self._orig_src
        self.tmp.cleanup()

    def _file(self, rel, body):
        p = self.root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(body, encoding="utf-8")

    def _base_entity(self, svc, layer="Core"):
        self._file(f"packages/microservices/{svc}/src/{svc}Service.{layer}/Entities/BaseEntity.cs",
                   "public abstract class BaseEntity\n{\n    public bool IsDeleted { get; set; } = false;\n}\n")

    def test_finds_services_declaring_is_deleted(self):
        self._base_entity("finance")
        self._base_entity("hr")
        self.assertEqual(v.services_with_is_deleted(), {"finance", "hr"})

    def test_zero_services_found_is_a_hard_failure(self):
        with self.assertRaises(SystemExit) as ctx:
            v.services_with_is_deleted()
        self.assertEqual(ctx.exception.code, 2)


class WriteDetectionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self._orig_src = v.SRC_GLOB
        v.SRC_GLOB = str(self.root / "packages/microservices/**/*.cs")

    def tearDown(self):
        v.SRC_GLOB = self._orig_src
        self.tmp.cleanup()

    def _file(self, rel, body):
        p = self.root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(body, encoding="utf-8")

    def test_a_real_write_in_a_repository_counts(self):
        self._file("packages/microservices/hse/src/HSEService.Infrastructure/Repositories/GenericRepository.cs",
                   "entity.IsDeleted = true;\n")
        self.assertTrue(v.has_write("hse"))

    def test_the_declaration_default_does_NOT_count(self):
        # `public bool IsDeleted { get; set; } = false;` in BaseEntity.cs is the same text shape as a
        # write (`IsDeleted = false`) but is not one — every instance gets it whether or not anything
        # was ever deleted. Without this exclusion the check would never find anything, because every
        # service satisfies this line trivially.
        self._file("packages/microservices/finance/src/FinanceService.Core/Entities/BaseEntity.cs",
                   "public bool IsDeleted { get; set; } = false;\n")
        self.assertFalse(v.has_write("finance"))

    def test_a_migration_declaring_the_column_does_NOT_count(self):
        self._file("packages/microservices/finance/src/FinanceService.Infrastructure/Migrations/20260101_Init.cs",
                   'IsDeleted = table.Column<bool>(type: "boolean", nullable: false)')
        self.assertFalse(v.has_write("finance"))

    def test_a_designer_snapshot_does_NOT_count(self):
        self._file("packages/microservices/finance/src/FinanceService.Infrastructure/Migrations/20260101_Init.Designer.cs",
                   'b.Property<bool>("IsDeleted")')
        self.assertFalse(v.has_write("finance"))

    def test_a_comparison_is_not_a_write(self):
        # `== true` is a read, not a write. A write_re that ignored the trailing `=` would misfire here.
        self._file("packages/microservices/finance/src/FinanceService.Core/Services/X.cs",
                   "if (entity.IsDeleted == true) return;")
        self.assertFalse(v.has_write("finance"))

    def test_no_write_anywhere_is_dead(self):
        self._file("packages/microservices/finance/src/FinanceService.Core/Services/X.cs",
                   "if (entity.IsDeleted) return Err(\"gone\");")
        self.assertFalse(v.has_write("finance"))


class RealRepoTests(unittest.TestCase):
    def test_the_baseline_matches_reality(self):
        services = v.services_with_is_deleted()
        dead = {svc for svc in services if not v.has_write(svc)}
        self.assertEqual(dead, set(v.KNOWN_DEAD),
                         f"new: {sorted(dead - set(v.KNOWN_DEAD))}, revived: {sorted(set(v.KNOWN_DEAD) - dead)}")

    def test_the_sweep_actually_sees_the_repo(self):
        # Guards against a glob or path change silently reducing coverage to a handful.
        self.assertGreaterEqual(len(v.services_with_is_deleted()), 10)


if __name__ == "__main__":
    unittest.main()
