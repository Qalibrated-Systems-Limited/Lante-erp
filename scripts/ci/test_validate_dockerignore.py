"""
Tests for validate_dockerignore.py — real fixture files under a temp directory, matching the convention
of the other validators here. Run with:
python3 -m unittest test_validate_dockerignore -v
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

import validate_dockerignore as v

WORKFLOW = """
name: Build & Deploy {svc}
jobs:
  build:
    steps:
      - uses: docker/build-push-action@v5
        with:
          context: ./{ctx}
          file: ./{ctx}/Dockerfile
"""


class DiscoveryTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        (self.root / ".github/workflows").mkdir(parents=True)

    def tearDown(self):
        self.tmp.cleanup()

    def _workflow(self, svc, ctx):
        (self.root / f".github/workflows/build-{svc}.yml").write_text(
            WORKFLOW.format(svc=svc, ctx=ctx), encoding="utf-8")

    def test_discovers_every_declared_context(self):
        self._workflow("a", "packages/microservices/a")
        self._workflow("b", "packages/microservices/b")
        found = v.discover_contexts(str(self.root / ".github/workflows/build-*.yml"))
        self.assertEqual(set(found), {"packages/microservices/a", "packages/microservices/b"})

    def test_the_leading_dot_slash_is_normalised_away(self):
        # Workflows write "./packages/..."; the filesystem check needs the bare path, and a mismatch here
        # would report every context as missing its .dockerignore.
        self._workflow("a", "packages/microservices/a")
        found = v.discover_contexts(str(self.root / ".github/workflows/build-*.yml"))
        self.assertIn("packages/microservices/a", found)

    def test_a_context_built_by_two_workflows_is_recorded_once(self):
        self._workflow("a", "packages/microservices/a")
        self._workflow("a-retry", "packages/microservices/a")
        found = v.discover_contexts(str(self.root / ".github/workflows/build-*.yml"))
        self.assertEqual(len(found), 1)
        self.assertEqual(len(found["packages/microservices/a"]), 2)

    def test_finding_no_contexts_is_a_hard_failure(self):
        # A checker that discovers nothing and reports success is the failure #198 exists for — and this
        # one would silently stop covering all sixteen builds if the `context:` key were ever renamed.
        with self.assertRaises(SystemExit) as ctx:
            v.discover_contexts(str(self.root / ".github/workflows/build-*.yml"))
        self.assertEqual(ctx.exception.code, 2)


class RealRepoTests(unittest.TestCase):
    """Against the actual tree, because the point is the actual builds."""

    def test_every_real_build_context_has_a_dockerignore(self):
        contexts = v.discover_contexts(v.WORKFLOWS)
        self.assertGreaterEqual(len(contexts), 10, "far fewer contexts than expected — parser stale?")
        for ctx in contexts:
            self.assertTrue((Path(v.REPO) / ctx / ".dockerignore").is_file(),
                            f"{ctx} has no .dockerignore; a root one does not cover it")

    def test_the_dotnet_copies_are_byte_identical(self):
        contexts = v.discover_contexts(v.WORKFLOWS)
        bodies = {c: (Path(v.REPO) / c / ".dockerignore").read_text(encoding="utf-8")
                  for c in contexts if c not in v.ALLOWED_DIFFERENT}
        # They exist only because .dockerignore is per-context. They are duplication, not variation.
        self.assertEqual(len(set(bodies.values())), 1, f"drifted: {sorted(bodies)}")

    def test_the_dotnet_copies_exclude_build_output(self):
        contexts = v.discover_contexts(v.WORKFLOWS)
        ctx = next(c for c in contexts if c not in v.ALLOWED_DIFFERENT)
        body = (Path(v.REPO) / ctx / ".dockerignore").read_text(encoding="utf-8")
        # The whole reason these exist: 106 MB of obj/ and bin/ was being uploaded per build, and a stale
        # artifact in bin/ can be copied in ahead of the fresh compile meant to replace it.
        self.assertIn("**/obj/", body)
        self.assertIn("**/bin/", body)

    def test_the_frontend_copy_excludes_node_modules(self):
        body = (Path(v.REPO) / "apps/lante_frontend/.dockerignore").read_text(encoding="utf-8")
        # 228 MB, and the image installs its own from the lockfile — a host-built native module reaching a
        # different base image is a real failure, not just wasted upload.
        self.assertIn("node_modules/", body)


if __name__ == "__main__":
    unittest.main()
