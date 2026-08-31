"""
Tests for validate_gateway_swagger_routes.py. Builds real fixture files under a temp directory
and drives main() end to end via a monkeypatched ROUTE_TO_PROGRAM_CS/YARP_JSON, covering the
three production-RoutePrefix shapes this codebase actually uses plus the real #225 bug.

Run with: python3 -m unittest test_validate_gateway_swagger_routes -v
"""
from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import validate_gateway_swagger_routes as mod

DUAL_BRANCH_SWAGGER_CS = """
    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerUI(c => { c.RoutePrefix = string.Empty; });
    }
    else
    {
        app.UseSwaggerUI(c => { c.RoutePrefix = "swagger"; });
    }
"""

UNCONDITIONAL_EMPTY_CS = """
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = string.Empty;
});
"""

DEFAULT_SWAGGER_CS = """
app.UseSwaggerUI();
"""

NO_SWAGGER_AT_ALL_CS = """
app.MapControllers();
"""


def yarp_json(pattern):
    return json.dumps({
        "ReverseProxy": {
            "Routes": {
                "swagger-thing-service": {
                    "ClusterId": "cluster-lante-thing-service",
                    "Match": {"Path": "/swagger/thing/{**catch-all}", "Methods": ["GET"]},
                    "Transforms": [{"PathPattern": pattern}],
                    "AuthorizationPolicy": "Anonymous",
                }
            }
        }
    })


class ProductionRoutePrefixTests(unittest.TestCase):
    def test_dual_branch_shape_returns_swagger(self):
        self.assertEqual(mod.production_route_prefix(DUAL_BRANCH_SWAGGER_CS, "x"), "swagger")

    def test_unconditional_empty_shape_returns_empty(self):
        self.assertEqual(mod.production_route_prefix(UNCONDITIONAL_EMPTY_CS, "x"), "")

    def test_bare_useswaggerui_defaults_to_swagger(self):
        self.assertEqual(mod.production_route_prefix(DEFAULT_SWAGGER_CS, "x"), "swagger")

    def test_no_swagger_call_at_all_is_a_hard_failure(self):
        with self.assertRaises(SystemExit) as ctx:
            mod.production_route_prefix(NO_SWAGGER_AT_ALL_CS, "x")
        self.assertEqual(ctx.exception.code, 2)

    def test_an_unexpected_conditional_shape_is_a_hard_failure(self):
        weird = DUAL_BRANCH_SWAGGER_CS.replace('"swagger"', '"docs"')
        with self.assertRaises(SystemExit) as ctx:
            mod.production_route_prefix(weird, "x")
        self.assertEqual(ctx.exception.code, 2)


class SwaggerRoutesParserTests(unittest.TestCase):
    def test_parses_a_single_route(self):
        routes = mod.swagger_routes(yarp_json("/swagger/{**catch-all}"))
        self.assertEqual(routes, {"swagger-thing-service": "/swagger/{**catch-all}"})

    def test_non_swagger_routes_are_ignored(self):
        src = json.dumps({
            "ReverseProxy": {"Routes": {
                "thing-service": {"ClusterId": "x", "Match": {"Path": "/api/thing"}},
            }}
        })
        with self.assertRaises(SystemExit) as ctx:
            mod.swagger_routes(src)
        self.assertEqual(ctx.exception.code, 2)


class MainEndToEndTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        (self.root / "svc").mkdir(parents=True)

    def tearDown(self):
        self.tmp.cleanup()

    def _write(self, program_cs_src, yarp_pattern):
        (self.root / "svc/Program.cs").write_text(program_cs_src)
        (self.root / "yarp.json").write_text(yarp_json(yarp_pattern))

    def _patch(self):
        return mock.patch.multiple(
            mod,
            YARP_JSON=str(self.root / "yarp.json"),
            ROUTE_TO_PROGRAM_CS={"swagger-thing-service": "svc/Program.cs"},
            REPO=str(self.root),
        )

    def test_correct_rewrite_for_a_swagger_prefixed_service_is_clean(self):
        self._write(DUAL_BRANCH_SWAGGER_CS, "/swagger/{**catch-all}")
        with self._patch():
            self.assertEqual(mod.main(), 0)

    def test_correct_rewrite_for_a_root_prefixed_service_is_clean(self):
        self._write(UNCONDITIONAL_EMPTY_CS, "/{**catch-all}")
        with self._patch():
            self.assertEqual(mod.main(), 0)

    def test_the_real_225_bug_shape_is_caught(self):
        # A service whose Program.cs serves at RoutePrefix=swagger in production, but whose
        # yarp.json route strips the whole /swagger/ prefix instead of just the service segment.
        self._write(DUAL_BRANCH_SWAGGER_CS, "/{**catch-all}")
        with self._patch():
            self.assertEqual(mod.main(), 1)

    def test_a_route_with_no_program_cs_mapping_is_caught(self):
        self._write(DUAL_BRANCH_SWAGGER_CS, "/swagger/{**catch-all}")
        with mock.patch.multiple(mod, YARP_JSON=str(self.root / "yarp.json"),
                                  ROUTE_TO_PROGRAM_CS={}, REPO=str(self.root)):
            self.assertEqual(mod.main(), 1)


if __name__ == "__main__":
    unittest.main()
