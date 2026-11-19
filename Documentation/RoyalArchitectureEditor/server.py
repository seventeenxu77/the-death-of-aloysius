"""Local, independent palace-design drafts. This server never writes to Unity assets."""
from __future__ import annotations

import argparse
import datetime as dt
import json
import math
import mimetypes
import os
from pathlib import Path
import re
import shutil
import sys
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import unquote, urlsplit
import uuid


SERVICE = "royal-architecture-editor"
DEFAULT_PORT = 8731
MAX_BODY = 2 * 1024 * 1024
HISTORY_LIMIT = 50
HISTORY_NAME = re.compile(r"project-r[0-9]{20}-[0-9]{8}T[0-9]{12}Z-[0-9a-f]{8}\.json\Z")


def now_iso() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


class ApiError(Exception):
    def __init__(self, status: int, code: str, message: str, **extra):
        super().__init__(message)
        self.status, self.code, self.message, self.extra = status, code, message, extra


def fail(message: str, code: str = "invalid_project"):
    raise ApiError(400, code, message)


def within(path: Path, directory: Path) -> bool:
    try:
        path.resolve().relative_to(directory.resolve())
        return True
    except (ValueError, OSError, RuntimeError):
        return False


def reject_constant(value):
    fail("JSON 不能包含 NaN 或 Infinity。")


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            fail("JSON 包含重复字段：" + key[:100])
        result[key] = value
    return result


def parse_json(data: bytes):
    try:
        return json.loads(data.decode("utf-8-sig"), parse_constant=reject_constant, object_pairs_hook=unique_object)
    except (UnicodeError, json.JSONDecodeError, RecursionError):
        fail("请求必须是有效的 UTF-8 JSON。", "invalid_json")


def encoded_json(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, allow_nan=False, indent=2) + "\n").encode("utf-8")


def integer(value, label: str):
    if type(value) is not int or not 0 <= value <= 9007199254740991:
        fail(label + "必须是非负整数。")


def text_value(value, label: str, limit: int):
    if not isinstance(value, str) or len(value) > limit or "\x00" in value:
        fail(label + "必须是长度不超过 " + str(limit) + " 的字符串，且不能包含空字符。")


def vector(value, size: int, label: str, positive: bool = False):
    if not isinstance(value, list) or len(value) != size:
        fail(label + "必须包含 " + str(size) + " 个数字。")
    for number in value:
        if type(number) not in (int, float):
            fail(label + "只能包含有限数字。")
        try:
            finite = math.isfinite(number)
        except OverflowError:
            finite = False
        if not finite or (positive and number <= 0):
            fail(label + ("只能包含大于零的有限数字。" if positive else "只能包含有限数字。"))


class ProjectStore:
    def __init__(self, root: Path, state_dir: Path | None = None):
        self.root = root.resolve()
        self.state_dir = (state_dir or self.root / "data").resolve()
        if state_dir is None and not within(self.state_dir, self.root):
            raise ValueError("The default data directory must remain inside the editor directory.")
        self.project_path = self.state_dir / "project.json"
        self.history_dir = self.state_dir / "history"
        self.lock = threading.RLock()
        self.started_at = now_iso()

    def model(self):
        path = self.root / "model" / "scene.json"
        if not within(path, self.root) or not path.is_file():
            raise ApiError(503, "model_unavailable", "建筑模型尚未就绪：缺少 model/scene.json。请等待导出完成后刷新。")
        try:
            model = json.loads(path.read_text(encoding="utf-8-sig"))
            scene_id = model["sceneId"]
            nodes = model["nodes"]
            if not isinstance(scene_id, str) or not scene_id or len(scene_id) > 256:
                raise ValueError("invalid sceneId")
            if isinstance(nodes, list):
                ids = [node["id"] for node in nodes]
            elif isinstance(nodes, dict):
                ids = list(nodes)
            else:
                raise ValueError("invalid nodes")
            if any(not isinstance(item, str) or not item or len(item) > 256 for item in ids) or len(ids) != len(set(ids)):
                raise ValueError("invalid node IDs")
            return scene_id, set(ids)
        except (OSError, ValueError, KeyError, TypeError):
            raise ApiError(503, "model_invalid", "model/scene.json 无法读取或结构无效；尚未创建或覆盖任何方案。")

    def default(self, scene_id: str):
        return {"schemaVersion": 1, "sceneId": scene_id, "revision": 0,
                "updatedAt": self.started_at, "overrides": {}, "notes": ""}

    def validate(self, value, scene_id: str, node_ids: set[str], incoming: bool):
        if not isinstance(value, dict):
            fail("方案必须是 JSON 对象。")
        allowed = {"schemaVersion", "sceneId", "revision", "updatedAt", "overrides", "notes"}
        if incoming:
            allowed.add("baseRevision")
        extra = set(value) - allowed
        if extra:
            fail("方案包含未知字段：" + ", ".join(sorted(extra)))
        required = {"schemaVersion", "sceneId", "overrides", "notes", "baseRevision" if incoming else "revision"}
        if not required.issubset(value):
            fail("方案缺少字段：" + ", ".join(sorted(required - set(value))))
        if type(value["schemaVersion"]) is not int or value["schemaVersion"] != 1:
            fail("仅支持 schemaVersion 1。", "unsupported_schema")
        if value["sceneId"] != scene_id:
            raise ApiError(409, "scene_mismatch", "方案与当前建筑模型不对应。请重新载入当前模型后保存。")
        if "revision" in value:
            integer(value["revision"], "revision ")
        if "updatedAt" in value:
            text_value(value["updatedAt"], "updatedAt ", 80)
        if incoming:
            integer(value["baseRevision"], "baseRevision ")
        text_value(value["notes"], "方案备注", 20000)
        overrides = value["overrides"]
        if not isinstance(overrides, dict) or len(overrides) > min(20000, len(node_ids)):
            fail("overrides 必须是以现有模型对象 ID 为键的对象。")
        fields = {"position", "rotation", "scale", "name", "hidden", "note"}
        for node_id, override in overrides.items():
            if node_id not in node_ids:
                fail("模型中不存在对象 ID：" + str(node_id)[:100], "unknown_node")
            if not isinstance(override, dict):
                fail("对象覆盖值必须是 JSON 对象：" + node_id)
            unknown = set(override) - fields
            if unknown:
                fail("对象包含未知字段：" + ", ".join(sorted(unknown)))
            if "position" in override:
                vector(override["position"], 3, "position ")
            if "rotation" in override:
                vector(override["rotation"], 4, "rotation ")
                try:
                    norm = math.sqrt(sum(v * v for v in override["rotation"]))
                except OverflowError:
                    norm = float("inf")
                if not math.isfinite(norm) or abs(norm - 1.0) > 0.001:
                    fail("rotation 必须是归一化四元数 [x,y,z,w]，长度误差不超过 0.001。")
            if "scale" in override:
                vector(override["scale"], 3, "scale ", positive=True)
            if "name" in override:
                text_value(override["name"], "对象名称", 200)
            if "note" in override:
                text_value(override["note"], "对象备注", 4000)
            if "hidden" in override and type(override["hidden"]) is not bool:
                fail("hidden 必须是 true 或 false。")

    def safe_storage_path(self, path: Path):
        try:
            # state_dir was canonicalized once at startup: do not resolve the base
            # again and accidentally approve a directory link swapped in later.
            path.resolve().relative_to(self.state_dir)
        except (ValueError, OSError, RuntimeError):
            raise ApiError(500, "unsafe_storage", "方案目录包含指向外部的链接，已停止写入。")

    def read_locked(self, scene_id: str, ids: set[str]):
        self.safe_storage_path(self.project_path)
        if not self.project_path.exists():
            return self.default(scene_id)
        try:
            if self.project_path.stat().st_size > MAX_BODY:
                raise ValueError("too large")
            saved = parse_json(self.project_path.read_bytes())
            self.validate(saved, scene_id, ids, incoming=False)
            return saved
        except ApiError as error:
            if error.code == "scene_mismatch":
                raise
            raise ApiError(500, "stored_project_invalid", "已保存方案的格式不正确。原文件已保留，请先备份并检查 data/project.json。")
        except (OSError, ValueError):
            raise ApiError(500, "stored_project_unreadable", "无法读取已保存方案。原文件未被覆盖。")

    def get(self):
        with self.lock:
            scene_id, ids = self.model()
            return self.read_locked(scene_id, ids)

    def atomic_write(self, path: Path, content: bytes):
        self.safe_storage_path(path)
        path.parent.mkdir(parents=True, exist_ok=True)
        self.safe_storage_path(path)
        temp_path = None
        try:
            with tempfile.NamedTemporaryFile(mode="wb", prefix=".royal-draft-", suffix=".tmp", dir=path.parent, delete=False) as stream:
                temp_path = Path(stream.name)
                stream.write(content)
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temp_path, path)
            temp_path = None
        finally:
            if temp_path and temp_path.exists():
                temp_path.unlink()

    def prune_history(self):
        self.safe_storage_path(self.history_dir)
        entries = sorted(p for p in self.history_dir.iterdir()
                         if HISTORY_NAME.fullmatch(p.name) and p.is_file() and not p.is_symlink() and within(p, self.history_dir))
        for path in entries[:-HISTORY_LIMIT]:
            # Only this server's archive filenames, inside this exact history directory.
            self.safe_storage_path(path)
            path.unlink()

    def put(self, incoming):
        with self.lock:
            scene_id, ids = self.model()
            self.validate(incoming, scene_id, ids, incoming=True)
            current = self.read_locked(scene_id, ids)
            if incoming["baseRevision"] != current["revision"]:
                raise ApiError(409, "revision_conflict", "方案已在其他窗口更新。请先重新载入最新方案，再保存你的修改。",
                               currentRevision=current["revision"])
            if current["revision"] >= 9007199254740991:
                raise ApiError(409, "revision_limit", "方案版本号已达到上限，请先导出备份。")
            saved = {"schemaVersion": 1, "sceneId": scene_id, "revision": current["revision"] + 1,
                     "updatedAt": now_iso(), "overrides": incoming["overrides"], "notes": incoming["notes"]}
            content = encoded_json(saved)
            if len(content) > MAX_BODY:
                fail("方案内容过大，请减少备注或覆盖项。", "project_too_large")
            stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
            archive = self.history_dir / (f"project-r{saved['revision']:020d}-{stamp}-{uuid.uuid4().hex[:8]}.json")
            try:
                self.atomic_write(archive, content)
                self.atomic_write(self.project_path, content)
            except OSError:
                raise ApiError(500, "save_failed", "方案写入失败，未确认保存成功。请检查目录权限和磁盘空间。")
            try:
                self.prune_history()
            except OSError as error:
                print("History pruning deferred: " + str(error), file=sys.stderr, flush=True)
            return saved


class EditorServer(ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = False

    def __init__(self, root: Path, port: int = DEFAULT_PORT, state_dir: Path | None = None):
        self.root = root.resolve()
        self.store = ProjectStore(self.root, state_dir)
        self.aliases = {
            "atlas": self.root / "references" / "atlas",
        }
        super().__init__(("127.0.0.1", port), EditorHandler)
        self.port = self.server_address[1]


class EditorHandler(BaseHTTPRequestHandler):
    server_version = "RoyalArchitectureEditor/1"
    sys_version = ""
    protocol_version = "HTTP/1.1"

    def setup(self):
        super().setup()
        self.connection.settimeout(15)

    def log_message(self, format, *args):
        # No payloads, names, or notes are written to logs.
        print(f"{self.log_date_time_string()} {format % args}", file=sys.stderr, flush=True)

    def validate_request(self, mutation: bool = False):
        permitted_hosts = {f"127.0.0.1:{self.server.port}", f"localhost:{self.server.port}"}
        if len(self.headers.get_all("Host", [])) != 1 or self.headers.get("Host", "").lower() not in permitted_hosts:
            raise ApiError(403, "invalid_host", "仅允许访问本机建筑编辑器地址。")
        origin = self.headers.get("Origin")
        allowed_origins = {"http://" + host for host in permitted_hosts}
        if len(self.headers.get_all("Origin", [])) > 1 or (origin is not None and origin not in allowed_origins):
            raise ApiError(403, "cross_origin_denied", "拒绝来自其他网站或端口的请求。")
        if mutation and origin is None and self.headers.get("Sec-Fetch-Site") in {"cross-site", "same-site"}:
            raise ApiError(403, "cross_site_denied", "拒绝跨站保存请求。")

    def common_headers(self):
        self.send_header("Cache-Control", "no-cache")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("X-Frame-Options", "DENY")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Cross-Origin-Resource-Policy", "same-origin")
        origin = self.headers.get("Origin")
        if origin in {f"http://127.0.0.1:{self.server.port}", f"http://localhost:{self.server.port}"}:
            self.send_header("Access-Control-Allow-Origin", origin)
            self.send_header("Vary", "Origin")

    def json_response(self, status: int, value, head: bool = False):
        content = encoded_json(value)
        self.send_response(status)
        self.common_headers()
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(content)))
        self.end_headers()
        if not head:
            self.wfile.write(content)

    def error_response(self, error: ApiError):
        # An unread request body must never be mistaken for a second request.
        self.close_connection = True
        self.json_response(error.status, {"error": error.code, "message": error.message, **error.extra}, self.command == "HEAD")

    def route_path(self):
        try:
            raw = urlsplit(self.path)
            if raw.scheme or raw.netloc:
                raise ValueError("absolute URL")
            path = unquote(raw.path, encoding="utf-8", errors="strict")
        except (UnicodeError, ValueError):
            raise ApiError(400, "invalid_path", "无效的请求路径。")
        if "\x00" in path or "\\" in path or any(part == ".." for part in path.split("/")):
            raise ApiError(403, "path_denied", "禁止访问目录之外的文件。")
        return path

    def do_GET(self):
        self.get_or_head(False)

    def do_HEAD(self):
        self.get_or_head(True)

    def get_or_head(self, head: bool):
        try:
            self.validate_request()
            path = self.route_path()
            if path == "/api/health":
                try:
                    scene_id, _ = self.server.store.model()
                    ready = True
                except ApiError:
                    scene_id, ready = None, False
                self.json_response(200, {"service": SERVICE, "version": 1, "port": self.server.port,
                                         "workspace": str(self.server.root), "sceneId": scene_id, "modelReady": ready}, head)
            elif path == "/api/project":
                self.json_response(200, self.server.store.get(), head)
            elif path.startswith("/api/"):
                raise ApiError(404, "not_found", "接口不存在。")
            else:
                self.static_file(path, head)
        except ApiError as error:
            self.error_response(error)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def static_file(self, path: str, head: bool):
        parts = [part for part in path.split("/") if part]
        base = self.server.root
        if parts and parts[0] in self.server.aliases:
            base = self.server.aliases[parts.pop(0)].resolve()
        elif parts and parts[0] in {"data", "logs", "tests", "__pycache__"}:
            raise ApiError(403, "private_path", "方案与服务内部文件不通过静态地址公开。")
        if any(part.startswith(".") or ":" in part for part in parts):
            raise ApiError(403, "path_denied", "该文件不可通过浏览器读取。")
        candidate = base.joinpath(*parts)
        if not within(candidate, base):
            raise ApiError(403, "path_denied", "禁止访问目录之外的文件。")
        if candidate.is_dir():
            candidate /= "index.html"
        if not within(candidate, base) or candidate.suffix.lower() in {".py", ".pyc", ".ps1", ".cmd", ".bat", ".log"}:
            raise ApiError(403, "private_path", "该文件不可通过浏览器读取。")
        if within(candidate, self.server.store.state_dir):
            raise ApiError(403, "private_path", "方案文件只能通过方案接口读取。")
        if not candidate.is_file():
            raise ApiError(404, "not_found", "文件不存在。")
        try:
            with candidate.open("rb") as stream:
                length = os.fstat(stream.fileno()).st_size
                content_type = mimetypes.guess_type(candidate.name)[0] or "application/octet-stream"
                if content_type.startswith("text/") or candidate.suffix.lower() in {".js", ".json", ".svg"}:
                    content_type += "; charset=utf-8"
                self.send_response(200)
                self.common_headers()
                self.send_header("Content-Type", content_type)
                self.send_header("Content-Length", str(length))
                self.end_headers()
                if not head:
                    shutil.copyfileobj(stream, self.wfile, length=64 * 1024)
        except OSError:
            self.close_connection = True

    def do_PUT(self):
        try:
            self.validate_request(mutation=True)
            if self.route_path() != "/api/project":
                raise ApiError(404, "not_found", "接口不存在。")
            if self.headers.get("Transfer-Encoding") or len(self.headers.get_all("Content-Length", [])) != 1:
                raise ApiError(411, "length_required", "保存请求必须提供唯一 Content-Length。")
            try:
                length = int(self.headers["Content-Length"])
            except ValueError:
                raise ApiError(400, "invalid_length", "Content-Length 无效。")
            if length < 1 or length > MAX_BODY:
                raise ApiError(413, "payload_too_large", "保存内容不得超过 2 MB。")
            if self.headers.get_content_type() != "application/json":
                raise ApiError(415, "content_type", "保存请求必须使用 application/json。")
            raw = self.rfile.read(length)
            if len(raw) != length:
                fail("请求内容不完整。", "incomplete_body")
            self.json_response(200, self.server.store.put(parse_json(raw)))
        except ApiError as error:
            self.error_response(error)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def do_OPTIONS(self):
        try:
            self.validate_request(mutation=True)
            if self.route_path() != "/api/project":
                raise ApiError(404, "not_found", "接口不存在。")
            if self.headers.get("Access-Control-Request-Method", "PUT") not in {"GET", "PUT"}:
                raise ApiError(405, "method_denied", "只允许读取或保存方案。")
            headers = {item.strip().lower() for item in self.headers.get("Access-Control-Request-Headers", "").split(",") if item.strip()}
            if headers - {"content-type"}:
                raise ApiError(403, "headers_denied", "请求包含不允许的跨域头。")
            self.send_response(204); self.common_headers()
            self.send_header("Access-Control-Allow-Methods", "GET, PUT, OPTIONS")
            self.send_header("Access-Control-Allow-Headers", "Content-Type")
            self.send_header("Content-Length", "0"); self.end_headers()
        except ApiError as error:
            self.error_response(error)


def main():
    parser = argparse.ArgumentParser(description="Royal Scene 本地独立建筑方案编辑器")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT, help="正常启动固定8731；其他端口仅供隔离测试")
    parser.add_argument("--state-dir", type=Path, help="隔离测试状态目录；正常使用编辑器目录下data")
    args = parser.parse_args()
    if not 1 <= args.port <= 65535:
        parser.error("port must be between 1 and 65535")
    root = Path(__file__).resolve().parent
    try:
        server = EditorServer(root, args.port, args.state_dir)
    except OSError as error:
        print(f"Cannot bind 127.0.0.1:{args.port}. No existing service was stopped or replaced. {error}", file=sys.stderr)
        return 2
    print(f"{SERVICE}: http://127.0.0.1:{server.port}/", flush=True)
    try:
        server.serve_forever(poll_interval=0.3)
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
