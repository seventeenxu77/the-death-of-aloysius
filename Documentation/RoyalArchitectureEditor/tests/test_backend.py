"""Isolated API tests: never touch the editor's real model or user's data directory."""
from concurrent.futures import ThreadPoolExecutor
import http.client
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import threading
import unittest

SPEC = importlib.util.spec_from_file_location("royal_editor_server", Path(__file__).resolve().parents[1] / "server.py")
backend = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(backend)


class BackendTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="royal-editor-backend-test-")
        self.temp_root = Path(self.temp.name).resolve()
        self.root = self.temp_root / "editor"
        self.state = self.temp_root / "isolated-state"
        (self.root / "model").mkdir(parents=True)
        (self.root / "model" / "scene.json").write_text(json.dumps({"sceneId": "test-scene-guid", "nodes": [{"id": "wall-a"}, {"id": "room-b"}]}), encoding="utf-8")
        (self.root / "index.html").write_text("<p>test editor</p>", encoding="utf-8")
        (self.temp_root / "secret.txt").write_text("outside-secret", encoding="utf-8")
        self.server = backend.EditorServer(self.root, port=0, state_dir=self.state)
        self.thread = threading.Thread(target=self.server.serve_forever, kwargs={"poll_interval": 0.02}, daemon=True)
        self.thread.start()

    def tearDown(self):
        self.server.shutdown(); self.server.server_close(); self.thread.join(timeout=2)
        # Only remove the task-created TemporaryDirectory, never the real project.
        target = Path(self.temp.name).resolve()
        self.assertEqual(target, self.temp_root)
        self.assertTrue(target.name.startswith("royal-editor-backend-test-"))
        self.temp.cleanup()

    def call(self, method="GET", path="/api/project", body=None, headers=None):
        connection = http.client.HTTPConnection("127.0.0.1", self.server.port, timeout=4)
        sent_headers = dict(headers or {})
        if body is not None:
            if not isinstance(body, bytes):
                body = json.dumps(body, ensure_ascii=False).encode("utf-8")
            sent_headers.setdefault("Content-Type", "application/json")
        connection.request(method, path, body=body, headers=sent_headers)
        response = connection.getresponse()
        raw = response.read()
        status, returned_headers = response.status, dict(response.getheaders())
        connection.close()
        try:
            value = json.loads(raw)
        except (ValueError, UnicodeError):
            value = raw
        return status, value, returned_headers

    def draft(self, base=0):
        return {"schemaVersion": 1, "sceneId": "test-scene-guid", "baseRevision": base, "revision": 999,
                "updatedAt": "ignored-client-clock", "notes": "隔离验证，不是用户方案",
                "overrides": {"wall-a": {"position": [1.5, 3, -4], "rotation": [0, 0, 0, 1],
                                          "scale": [1, 2, 1], "name": "墙", "hidden": False, "note": "测试"}}}

    def test_default_is_read_only_and_health_identifies_service(self):
        status, value, headers = self.call()
        self.assertEqual(status, 200); self.assertEqual(value["revision"], 0)
        self.assertEqual(value["overrides"], {}); self.assertFalse(self.state.exists())
        self.assertEqual(headers["Cache-Control"], "no-cache")
        status, health, _ = self.call(path="/api/health")
        self.assertEqual(status, 200); self.assertEqual(health["service"], backend.SERVICE)
        self.assertTrue(health["modelReady"])

    def test_save_restore_revision_conflict_and_override_removal(self):
        status, saved, _ = self.call("PUT", body=self.draft())
        self.assertEqual(status, 200); self.assertEqual(saved["revision"], 1)
        self.assertNotEqual(saved["updatedAt"], "ignored-client-clock")
        self.assertNotIn("baseRevision", saved)
        reloaded = backend.ProjectStore(self.root, self.state).get()
        self.assertEqual(saved, reloaded)
        status, error, _ = self.call("PUT", body=self.draft())
        self.assertEqual(status, 409); self.assertEqual(error["currentRevision"], 1)
        clear = self.draft(1); clear["overrides"] = {}
        status, saved, _ = self.call("PUT", body=clear)
        self.assertEqual(status, 200); self.assertEqual(saved["overrides"], {})
        self.assertEqual(saved["revision"], 2)

    def test_concurrent_saves_never_overwrite_each_other(self):
        with ThreadPoolExecutor(max_workers=2) as pool:
            statuses = list(pool.map(lambda _: self.call("PUT", body=self.draft())[0], range(2)))
        self.assertEqual(sorted(statuses), [200, 409])
        self.assertEqual(self.server.store.get()["revision"], 1)

    def test_invalid_schema_and_override_values_do_not_create_data(self):
        changes = [
            lambda d: d.update(schemaVersion=2),
            lambda d: d.update(unexpected="no"),
            lambda d: d["overrides"].update({"missing": {}}),
            lambda d: d["overrides"]["wall-a"].update(scale=[1, 0, 1]),
            lambda d: d["overrides"]["wall-a"].update(scale=[1, -1, 1]),
            lambda d: d["overrides"]["wall-a"].update(rotation=[0, 0, 0]),
            lambda d: d["overrides"]["wall-a"].update(rotation=[0, 0, 0, 2]),
            lambda d: d["overrides"]["wall-a"].update(position=[1, True, 2]),
            lambda d: d["overrides"]["wall-a"].update(position=[float("nan"), 0, 0]),
            lambda d: d["overrides"]["wall-a"].update(hidden=1),
            lambda d: d["overrides"]["wall-a"].update(name="x" * 201),
            lambda d: d["overrides"]["wall-a"].update(material="arbitrary"),
        ]
        for change in changes:
            with self.subTest(change=change):
                draft = self.draft(); change(draft)
                self.assertEqual(self.call("PUT", body=draft)[0], 400)
        self.assertFalse(self.state.exists())

    def test_scene_mismatch_rejected_without_writing(self):
        draft = self.draft(); draft["sceneId"] = "different-scene"
        status, value, _ = self.call("PUT", body=draft)
        self.assertEqual(status, 409); self.assertEqual(value["error"], "scene_mismatch")
        self.assertFalse(self.state.exists())

    def test_missing_model_is_503_and_never_creates_wrong_default(self):
        (self.root / "model" / "scene.json").unlink()
        self.assertEqual(self.call()[0], 503)
        self.assertEqual(self.call("PUT", body=self.draft())[0], 503)
        self.assertFalse(self.call(path="/api/health")[1]["modelReady"])
        self.assertFalse(self.state.exists())

    def test_cross_site_and_host_checks(self):
        self.assertEqual(self.call("PUT", body=self.draft(), headers={"Origin": "https://attacker.example"})[0], 403)
        self.assertEqual(self.call("PUT", body=self.draft(), headers={"Origin": "null"})[0], 403)
        self.assertEqual(self.call("PUT", body=self.draft(), headers={"Sec-Fetch-Site": "cross-site"})[0], 403)
        self.assertEqual(self.call(headers={"Host": f"attacker.example:{self.server.port}"})[0], 403)
        origin = f"http://localhost:{self.server.port}"
        status, _, headers = self.call("PUT", body=self.draft(), headers={"Origin": origin})
        self.assertEqual(status, 200); self.assertEqual(headers["Access-Control-Allow-Origin"], origin)

    def test_path_traversal_private_files_and_aliases(self):
        for path in ["/%2e%2e/secret.txt", "/model/%5c..%5csecret.txt", "/data/project.json", "/server.py", "/logs/server.log"]:
            with self.subTest(path=path): self.assertEqual(self.call(path=path)[0], 403)
        self.assertEqual(self.call(path="/")[0], 200)
        atlas = self.temp_root / "RoyalSectionAtlas_OpenPassages_20261107"
        atlas.mkdir(); (atlas / "test.txt").write_text("atlas", encoding="utf-8")
        self.assertEqual(self.call(path="/atlas/test.txt")[1], b"atlas")
        self.assertEqual(self.call("PUT", "/atlas/test.txt", body={})[0], 404)
        self.assertEqual((atlas / "test.txt").read_text(), "atlas")

    def test_duplicate_json_and_oversized_payload_rejected(self):
        self.assertEqual(self.call("PUT", body=b'{"schemaVersion":1,"schemaVersion":1}')[0], 400)
        self.assertEqual(self.call("PUT", body=b"{}", headers={"Content-Length": str(backend.MAX_BODY + 1)})[0], 413)
        self.assertFalse(self.state.exists())

    def test_history_is_bounded_and_preserves_unrelated_files(self):
        history = self.state / "history"; history.mkdir(parents=True)
        keep = history / "user-notes.json"; keep.write_text("do not delete", encoding="utf-8")
        for revision in range(55): self.server.store.put(self.draft(revision))
        archives = [p for p in history.iterdir() if backend.HISTORY_NAME.fullmatch(p.name)]
        self.assertEqual(len(archives), 50); self.assertEqual(keep.read_text(), "do not delete")
        self.assertEqual(self.server.store.get()["revision"], 55)

    def test_existing_port_is_not_replaced(self):
        with self.assertRaises(OSError): backend.EditorServer(self.root, self.server.port, self.temp_root / "unused")
        self.assertEqual(self.call(path="/api/health")[0], 200)
        self.assertFalse((self.temp_root / "unused").exists())


if __name__ == "__main__":
    unittest.main()
