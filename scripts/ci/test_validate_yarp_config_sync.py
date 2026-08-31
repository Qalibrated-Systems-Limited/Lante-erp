"""
Tests for validate_yarp_config_sync.py. Run with:
python3 -m unittest test_validate_yarp_config_sync -v
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
from unittest import mock

import validate_yarp_config_sync as mod


class MainTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _patch(self, source, chart_copy):
        return mock.patch.multiple(mod, SOURCE=str(source), CHART_COPY=str(chart_copy))

    def test_identical_files_are_clean(self):
        src = self.root / "src.json"
        dst = self.root / "dst.json"
        src.write_text('{"a": 1}')
        dst.write_text('{"a": 1}')
        with self._patch(src, dst):
            self.assertEqual(mod.main(), 0)

    def test_diverged_files_are_caught(self):
        src = self.root / "src.json"
        dst = self.root / "dst.json"
        src.write_text('{"a": 1}')
        dst.write_text('{"a": 2}')
        with self._patch(src, dst):
            self.assertEqual(mod.main(), 1)

    def test_whitespace_only_difference_is_still_caught(self):
        # Deliberately byte-identical, not semantically-equal — the sync step is a plain file
        # copy, so anything that bypassed it (including a reformat) is exactly what this must
        # catch, not wave through because the JSON still parses the same way.
        src = self.root / "src.json"
        dst = self.root / "dst.json"
        src.write_text('{"a": 1}')
        dst.write_text('{"a":1}')
        with self._patch(src, dst):
            self.assertEqual(mod.main(), 1)

    def test_a_missing_file_is_a_hard_failure(self):
        src = self.root / "src.json"
        dst = self.root / "does-not-exist.json"
        src.write_text('{"a": 1}')
        with self._patch(src, dst):
            with self.assertRaises(SystemExit) as ctx:
                mod.main()
            self.assertEqual(ctx.exception.code, 2)


if __name__ == "__main__":
    unittest.main()
