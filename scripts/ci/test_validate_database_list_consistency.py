"""
Tests for validate_database_list_consistency.py — builds real fixture files under a temp
directory for each scenario rather than mocking. Run with:
python3 -m unittest test_validate_database_list_consistency -v
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from validate_database_list_consistency import (
    check_no_stray_hardcoded_lists,
    find_consistency_problems,
    find_generate_secrets_databases,
    find_source_of_truth_databases,
)

VALUES_YAML_TEMPLATE = """
postgresql:
  primary:
    initdb:
      scripts:
        create-databases-and-app-role.sh: |
          #!/bin/bash
          DATABASES="{databases}"
          for db in $DATABASES; do
            psql -c "CREATE DATABASE $db;"
          done
"""


class DatabaseListConsistencyTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.values_yaml = self.root / "values.yaml"
        self.generate_secrets_sh = self.root / "generate-secrets.sh"
        self.kubernetes_dir = self.root / "kubernetes"
        self.kubernetes_dir.mkdir()

    def tearDown(self):
        self.tmp.cleanup()

    def _write_values_yaml(self, databases: list[str]):
        self.values_yaml.write_text(VALUES_YAML_TEMPLATE.format(databases=" ".join(databases)))

    def _write_generate_secrets(self, databases: list[str]):
        lines = [f'{db.upper()}_DB_CONNECTION="Host=x;Database={db};Username=postgres"' for db in databases]
        lines += [f'{db.upper()}_APP_CONNECTION="Host=x;Database={db};Username=app"' for db in databases]
        self.generate_secrets_sh.write_text("\n".join(lines))

    def _run(self):
        return find_consistency_problems(
            values_yaml=self.values_yaml,
            generate_secrets_sh=self.generate_secrets_sh,
            kubernetes_dir=self.kubernetes_dir,
            repo_root=self.root,
        )

    def test_find_source_of_truth_databases_parses_the_list(self):
        self._write_values_yaml(["lante_userservice", "lante_finance"])
        self.assertEqual(find_source_of_truth_databases(self.values_yaml), {"lante_userservice", "lante_finance"})

    def test_find_generate_secrets_databases_parses_both_connection_kinds(self):
        self._write_generate_secrets(["lante_userservice", "lante_finance"])
        self.assertEqual(
            find_generate_secrets_databases(self.generate_secrets_sh),
            {"lante_userservice", "lante_finance"},
        )

    def test_fully_consistent_state_has_no_problems(self):
        dbs = ["lante_userservice", "lante_finance", "lante_crm"]
        self._write_values_yaml(dbs)
        self._write_generate_secrets(dbs)

        self.assertEqual(self._run(), [])

    def test_database_missing_from_generate_secrets_is_caught(self):
        # Today's real regression: lante_crm/lante_hr/lante_procurement existed in initdb's list
        # but generate-secrets.sh never built a connection string for them.
        self._write_values_yaml(["lante_userservice", "lante_finance", "lante_crm"])
        self._write_generate_secrets(["lante_userservice", "lante_finance"])

        problems = self._run()
        self.assertTrue(any("lante_crm" in p and "generate-secrets.sh" in p for p in problems))

    def test_stale_database_in_generate_secrets_is_caught(self):
        self._write_values_yaml(["lante_userservice", "lante_finance"])
        self._write_generate_secrets(["lante_userservice", "lante_finance", "lante_decommissioned"])

        problems = self._run()
        self.assertTrue(any("lante_decommissioned" in p for p in problems))

    def test_stray_hardcoded_list_outside_allowed_files_is_caught(self):
        self._write_values_yaml(["lante_userservice", "lante_finance", "lante_crm"])
        self._write_generate_secrets(["lante_userservice", "lante_finance", "lante_crm"])
        stray = self.kubernetes_dir / "backups" / "some-new-cronjob.yaml"
        stray.parent.mkdir(parents=True)
        stray.write_text('for db in lante_userservice lante_finance lante_crm; do echo "$db"; done')

        problems = self._run()
        self.assertTrue(any("some-new-cronjob.yaml" in p for p in problems))

    def test_stray_list_check_ignores_lines_with_fewer_than_three_names(self):
        stray = self.kubernetes_dir / "notes.yaml"
        stray.write_text("# see lante_userservice and lante_finance for examples")

        problems = check_no_stray_hardcoded_lists(self.kubernetes_dir, self.root, set())
        self.assertEqual(problems, [])

    def test_stray_list_check_skips_allowed_files_even_if_inside_kubernetes_dir(self):
        allowed = self.kubernetes_dir / "values.yaml"
        allowed.write_text("DATABASES=lante_a lante_b lante_c")

        problems = check_no_stray_hardcoded_lists(self.kubernetes_dir, self.root, {allowed})
        self.assertEqual(problems, [])


if __name__ == "__main__":
    unittest.main()
