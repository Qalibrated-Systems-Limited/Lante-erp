"""
Tests for validate_designtime_switch_mirror.py. Builds a fake repo layout under a temp
directory and drives main() end to end via monkeypatched globals.

Run with: python3 -m unittest test_validate_designtime_switch_mirror -v
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
from unittest import mock

import validate_designtime_switch_mirror as mod

PROGRAM_CS_WITH_SWITCH = '''
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);
'''

PROGRAM_CS_NO_SWITCH = '''
var builder = WebApplication.CreateBuilder(args);
'''

FACTORY_WITH_SWITCH = '''
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FooDbContext>
{
    public FooDbContext CreateDbContext(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        return null;
    }
}
'''

FACTORY_WITHOUT_SWITCH = '''
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FooDbContext>
{
    public FooDbContext CreateDbContext(string[] args)
    {
        return null;
    }
}
'''


class ParserTests(unittest.TestCase):
    def test_parses_a_switch(self):
        self.assertEqual(mod.switches_in(PROGRAM_CS_WITH_SWITCH),
                          {("Npgsql.EnableLegacyTimestampBehavior", "true")})

    def test_no_switch_is_empty_not_an_error(self):
        self.assertEqual(mod.switches_in(PROGRAM_CS_NO_SWITCH), set())


class MainEndToEndTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        (self.root / "packages/microservices/foo/src/FooService.Api").mkdir(parents=True)
        (self.root / "packages/microservices/foo/src/FooService.Infrastructure/Data").mkdir(parents=True)

    def tearDown(self):
        self.tmp.cleanup()

    def _patch(self, known_gaps=None):
        return mock.patch.multiple(
            mod,
            REPO=str(self.root),
            PROGRAM_CS_GLOB=str(self.root / "packages/microservices/**/Program.cs"),
            KNOWN_GAPS={} if known_gaps is None else known_gaps,
        )

    def test_a_service_with_no_switch_is_not_checked_at_all(self):
        (self.root / "packages/microservices/foo/src/FooService.Api/Program.cs").write_text(PROGRAM_CS_NO_SWITCH)
        (self.root / "packages/microservices/foo/src/FooService.Infrastructure/Data/DesignTimeDbContextFactory.cs").write_text(FACTORY_WITHOUT_SWITCH)
        with self._patch():
            self.assertEqual(mod.main(), 0)

    def test_a_mirrored_switch_is_clean(self):
        (self.root / "packages/microservices/foo/src/FooService.Api/Program.cs").write_text(PROGRAM_CS_WITH_SWITCH)
        (self.root / "packages/microservices/foo/src/FooService.Infrastructure/Data/DesignTimeDbContextFactory.cs").write_text(FACTORY_WITH_SWITCH)
        with self._patch():
            self.assertEqual(mod.main(), 0)

    def test_a_missing_switch_is_caught(self):
        (self.root / "packages/microservices/foo/src/FooService.Api/Program.cs").write_text(PROGRAM_CS_WITH_SWITCH)
        (self.root / "packages/microservices/foo/src/FooService.Infrastructure/Data/DesignTimeDbContextFactory.cs").write_text(FACTORY_WITHOUT_SWITCH)
        with self._patch():
            self.assertEqual(mod.main(), 1)

    def test_a_known_gap_is_not_a_failure(self):
        (self.root / "packages/microservices/foo/src/FooService.Api/Program.cs").write_text(PROGRAM_CS_WITH_SWITCH)
        factory = self.root / "packages/microservices/foo/src/FooService.Infrastructure/Data/DesignTimeDbContextFactory.cs"
        factory.write_text(FACTORY_WITHOUT_SWITCH)
        rel = str(factory.relative_to(self.root))
        with self._patch(known_gaps={rel: "test reason"}):
            self.assertEqual(mod.main(), 0)

    def test_a_stale_known_gap_is_a_failure(self):
        # The KNOWN_GAPS entry claims a gap that no longer exists — the baseline must shrink.
        (self.root / "packages/microservices/foo/src/FooService.Api/Program.cs").write_text(PROGRAM_CS_WITH_SWITCH)
        (self.root / "packages/microservices/foo/src/FooService.Infrastructure/Data/DesignTimeDbContextFactory.cs").write_text(FACTORY_WITH_SWITCH)
        with self._patch(known_gaps={"some/stale/path.cs": "no longer true"}):
            self.assertEqual(mod.main(), 1)

    def test_a_missing_factory_entirely_is_caught(self):
        (self.root / "packages/microservices/foo/src/FooService.Api/Program.cs").write_text(PROGRAM_CS_WITH_SWITCH)
        # No factory file written at all.
        with self._patch():
            self.assertEqual(mod.main(), 1)


if __name__ == "__main__":
    unittest.main()
