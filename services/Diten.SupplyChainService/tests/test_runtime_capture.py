"""Exercise the real call() function without starting the process-level scenario."""
import ast
import io
import json
import pathlib
import unittest
import urllib.request
import urllib.error
from unittest.mock import patch


class RuntimeCaptureTests(unittest.TestCase):
    def setUp(self):
        path = pathlib.Path(__file__).with_name("runtime_probe.py")
        tree = ast.parse(path.read_text())
        call = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == "call")
        self.scope = dict(json=json, urllib=urllib, T="tenant", L="entity", C="correlation",
                          A="actor", PERMS=[], SECRET="fixture", records=[],
                          token=lambda *args: "fixture", validate=lambda *args: None)
        exec(compile(ast.Module(body=[call], type_ignores=[]), str(path), "exec"), self.scope)
        self.sent = []

    def respond(self, request):
        self.sent.append(request.data)
        response = io.BytesIO(b'{}')
        response.status = 200
        return response

    def test_nested_request_snapshot_matches_sent_bytes_after_caller_mutation(self):
        body = {"lines": [{"quantity": "٣٠.٠٠٠"}], "note": "😀" * 1001}
        with patch("urllib.request.urlopen", side_effect=self.respond):
            self.scope["call"]("POST", body=body)
        sent = json.loads(self.sent[-1])
        body["lines"][0]["quantity"] = "30.000"
        body["note"] = "😀" * 1000
        self.assertEqual(sent, self.scope["records"][-1]["request"])
        self.assertEqual("٣٠.٠٠٠", self.scope["records"][-1]["request"]["lines"][0]["quantity"])
        self.assertEqual(1001, len(self.scope["records"][-1]["request"]["note"]))

    def test_absent_body_remains_null(self):
        with patch("urllib.request.urlopen", side_effect=self.respond):
            self.scope["call"]("GET")
        self.assertIsNone(self.sent[-1])
        self.assertIsNone(self.scope["records"][-1]["request"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
