"""Per-agent MCP endpoint and authentication boundary."""

from __future__ import annotations

import hmac
import uuid
from typing import Any, Callable

from fastmcp import FastMCP
from starlette.responses import PlainTextResponse

from hub.bridge import BridgeError, UnityBridge
from hub.store import HubStore, StoreError


class AuthenticatedEndpoint:
    def __init__(self, app: Any, *, token: str, port: int,
                 on_authenticated: Callable[[], None] | None = None):
        self.app = app
        self.token = token
        self.port = port
        self.on_authenticated = on_authenticated

    async def __call__(self, scope: dict, receive: Any, send: Any) -> None:
        if scope["type"] == "lifespan":
            await self.app(scope, receive, send)
            return
        if scope["type"] != "http":
            await PlainTextResponse("Forbidden", status_code=403)(scope, receive, send)
            return

        headers = {key.lower(): value for key, value in scope.get("headers", [])}
        host = headers.get(b"host", b"").decode("ascii", errors="ignore").lower()
        allowed_hosts = {f"127.0.0.1:{self.port}", f"localhost:{self.port}"}
        origin = headers.get(b"origin", b"").decode("ascii", errors="ignore").lower()
        allowed_origins = {f"http://{host}" for host in allowed_hosts}
        if host not in allowed_hosts or (origin and origin not in allowed_origins):
            await PlainTextResponse("Forbidden", status_code=403)(scope, receive, send)
            return

        authorization = headers.get(b"authorization", b"")
        expected = f"Bearer {self.token}".encode("utf-8")
        if not hmac.compare_digest(authorization, expected):
            await PlainTextResponse("Unauthorized", status_code=401)(scope, receive, send)
            return
        if self.on_authenticated is not None:
            self.on_authenticated()
        await self.app(scope, receive, send)


def create_endpoint(agent_id: str, token: str, port: int,
                    bridge: UnityBridge, store: HubStore,
                    on_authenticated: Callable[[], None] | None = None) -> AuthenticatedEndpoint:
    mcp = FastMCP(name=f"Novahn Hub ({agent_id})")

    def stored(operation: Any, *args: Any) -> dict[str, Any]:
        correlation_id = str(uuid.uuid4())
        try:
            value = operation(agent_id, *args, correlation_id)
            return {"status": "succeeded", "agentId": agent_id,
                    "correlationId": correlation_id, "data": value}
        except StoreError as exc:
            return {"status": "failed", "agentId": agent_id,
                    "correlationId": correlation_id,
                    "error": {"code": exc.code, "message": str(exc)}}

    @mcp.tool(name="hub.health", annotations={"readOnlyHint": True})
    async def health() -> dict[str, Any]:
        return {"status": "ready", "agentId": agent_id,
                "correlationId": str(uuid.uuid4())}

    @mcp.tool(name="hub.project.info", annotations={"readOnlyHint": True})
    async def project_info() -> dict[str, Any]:
        correlation_id = str(uuid.uuid4())
        try:
            instances = await bridge.instances()
            return {"status": "succeeded", "agentId": agent_id,
                    "correlationId": correlation_id, "instances": instances}
        except BridgeError as exc:
            return {"status": "failed", "agentId": agent_id,
                    "correlationId": correlation_id,
                    "error": {"code": exc.code, "message": str(exc)}}

    @mcp.tool(name="unity.scenes.list", annotations={"readOnlyHint": True})
    async def list_scenes(unity_instance: str | None = None) -> dict[str, Any]:
        correlation_id = str(uuid.uuid4())
        try:
            result = await bridge.loaded_scenes(unity_instance)
            return {"status": "succeeded", "agentId": agent_id,
                    "correlationId": correlation_id, "data": result.get("data", result)}
        except BridgeError as exc:
            return {"status": "failed", "agentId": agent_id,
                    "correlationId": correlation_id,
                    "error": {"code": exc.code, "message": str(exc)}}

    @mcp.tool(name="hub.task.create")
    async def create_task(assignee: str, request: str,
                          idempotency_key: str) -> dict[str, Any]:
        return stored(store.create_task, assignee, request, idempotency_key)

    @mcp.tool(name="hub.task.inbox", annotations={"readOnlyHint": True})
    async def task_inbox() -> dict[str, Any]:
        return {"status": "succeeded", "agentId": agent_id,
                "correlationId": str(uuid.uuid4()),
                "data": store.task_inbox(agent_id)}

    @mcp.tool(name="hub.task.get", annotations={"readOnlyHint": True})
    async def get_task(task_id: str) -> dict[str, Any]:
        correlation_id = str(uuid.uuid4())
        try:
            return {"status": "succeeded", "agentId": agent_id,
                    "correlationId": correlation_id,
                    "data": store.get_task(agent_id, task_id)}
        except StoreError as exc:
            return {"status": "failed", "agentId": agent_id,
                    "correlationId": correlation_id,
                    "error": {"code": exc.code, "message": str(exc)}}

    @mcp.tool(name="hub.task.claim")
    async def claim_task(task_id: str) -> dict[str, Any]:
        return stored(store.claim_task, task_id)

    @mcp.tool(name="hub.task.complete")
    async def complete_task(task_id: str, result: str) -> dict[str, Any]:
        return stored(store.complete_task, task_id, result)

    @mcp.tool(name="hub.task.cancel")
    async def cancel_task(task_id: str) -> dict[str, Any]:
        return stored(store.cancel_task, task_id)

    @mcp.tool(name="hub.message.send")
    async def send_message(recipient: str, body: str, idempotency_key: str,
                           task_id: str | None = None) -> dict[str, Any]:
        correlation_id = str(uuid.uuid4())
        try:
            value = store.send_message(agent_id, recipient, body,
                                       idempotency_key, correlation_id, task_id)
            return {"status": "succeeded", "agentId": agent_id,
                    "correlationId": correlation_id, "data": value}
        except StoreError as exc:
            return {"status": "failed", "agentId": agent_id,
                    "correlationId": correlation_id,
                    "error": {"code": exc.code, "message": str(exc)}}

    @mcp.tool(name="hub.message.inbox", annotations={"readOnlyHint": True})
    async def message_inbox() -> dict[str, Any]:
        return {"status": "succeeded", "agentId": agent_id,
                "correlationId": str(uuid.uuid4()),
                "data": store.message_inbox(agent_id)}

    @mcp.tool(name="hub.message.ack")
    async def acknowledge_message(message_id: str) -> dict[str, Any]:
        return stored(store.acknowledge_message, message_id)

    return AuthenticatedEndpoint(mcp.http_app(path="/mcp", stateless_http=True),
                                 token=token, port=port,
                                 on_authenticated=on_authenticated)
