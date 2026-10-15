"""Restrict the existing Unity MCP ASGI app to its local bridge routes."""

from __future__ import annotations

import hmac
from typing import Any, Awaitable, Callable

from starlette.responses import JSONResponse, PlainTextResponse


class BridgeOnlyEndpoint:
    def __init__(self, app: Any, *, token: str, port: int,
                 dashboard: Callable[[], Awaitable[dict[str, Any]]] | None = None):
        self.app = app
        self.token = token
        self.port = port
        self.dashboard = dashboard

    async def __call__(self, scope: dict, receive: Any, send: Any) -> None:
        if scope["type"] == "lifespan":
            await self.app(scope, receive, send)
            return

        kind = scope["type"]
        path = scope.get("path", "")
        headers = {key.lower(): value for key, value in scope.get("headers", [])}
        host = headers.get(b"host", b"").decode("ascii", errors="ignore").lower()
        allowed_hosts = {f"127.0.0.1:{self.port}", f"localhost:{self.port}"}
        origin = headers.get(b"origin", b"").decode("ascii", errors="ignore").lower()
        if host not in allowed_hosts or (origin and origin not in {
                f"http://{value}" for value in allowed_hosts}):
            await self._deny(scope, receive, send, 403)
            return

        if kind == "websocket" and path == "/hub/plugin":
            await self.app(scope, receive, send)
            return
        if kind == "http" and path == "/health" and scope.get("method") == "GET":
            await self.app(scope, receive, send)
            return
        if kind == "http" and path == "/hub/status" and self.dashboard is not None:
            if scope.get("method") != "GET":
                await self._deny(scope, receive, send, 405)
                return
            await JSONResponse(await self.dashboard())(scope, receive, send)
            return
        if kind == "http" and path in {"/api/instances", "/api/command"}:
            expected_method = "GET" if path == "/api/instances" else "POST"
            if scope.get("method") != expected_method:
                await self._deny(scope, receive, send, 405)
                return
            supplied = headers.get(b"x-unity-hub-bridge-token", b"")
            if hmac.compare_digest(supplied, self.token.encode("utf-8")):
                await self.app(scope, receive, send)
                return
            await self._deny(scope, receive, send, 401)
            return
        await self._deny(scope, receive, send, 404)

    @staticmethod
    async def _deny(scope: dict, receive: Any, send: Any, status: int) -> None:
        if scope["type"] == "http":
            await PlainTextResponse("Unavailable", status_code=status)(scope, receive, send)
        else:
            await send({"type": "websocket.close", "code": 1008})
