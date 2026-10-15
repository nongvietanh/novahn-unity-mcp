"""The two client ports must keep identities and credentials separate."""

from __future__ import annotations

import asyncio
import json
import socket

import pytest
import uvicorn
from httpx import ASGITransport, AsyncClient, MockTransport, Response
from starlette.responses import PlainTextResponse

# Legacy integration tests replace fastmcp globally during collection. Run this
# module on its own for the real HTTP transport exercise in that test environment.
import fastmcp

if not hasattr(fastmcp, "Client"):
    pytest.skip("real FastMCP is stubbed by legacy integration fixtures",
                allow_module_level=True)

from fastmcp import Client

from hub.bridge import BridgeError, UnityBridge
from hub.config import AgentSpec, HubConfig
from hub.dashboard import HubDashboard
from hub.bridge_endpoint import BridgeOnlyEndpoint
from hub.endpoint import create_endpoint
from hub.store import HubStore


class FakeBridge:
    def __init__(self) -> None:
        self.scene_calls: list[str | None] = []

    async def instances(self) -> list[dict]:
        return [{"project": "Sample", "hash": "abc", "unity_version": "2022.3"}]

    async def loaded_scenes(self, unity_instance: str | None = None) -> dict:
        self.scene_calls.append(unity_instance)
        return {"success": True, "data": {"scenes": ["Assets/Sample.unity"]}}


def free_socket() -> socket.socket:
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.bind(("127.0.0.1", 0))
    sock.listen(5)
    return sock


@pytest.mark.asyncio
async def test_two_endpoints_isolate_identity_and_token() -> None:
    sockets = [free_socket(), free_socket()]
    ports = [sock.getsockname()[1] for sock in sockets]
    bridge = FakeBridge()
    store = HubStore(":memory:")
    tokens = ["a" * 40, "b" * 40]
    servers = [uvicorn.Server(uvicorn.Config(
        create_endpoint(agent, token, port, bridge, store),
        host="127.0.0.1", port=port, log_level="error", access_log=False,
    )) for agent, token, port in zip(("codex", "claude"), tokens, ports)]
    for server in servers:
        server.install_signal_handlers = lambda: None
    tasks = [asyncio.create_task(server.serve(sockets=[sock]))
             for server, sock in zip(servers, sockets)]
    try:
        async with asyncio.timeout(5):
            while not all(server.started for server in servers):
                await asyncio.sleep(0.01)

        async with Client(f"http://127.0.0.1:{ports[0]}/mcp", auth=tokens[0]) as codex:
            names = [tool.name for tool in await codex.list_tools()]
            assert {"hub.health", "hub.project.info", "unity.scenes.list",
                    "hub.task.create", "hub.task.claim", "hub.task.complete",
                    "hub.task.cancel",
                    "hub.task.inbox", "hub.task.get", "hub.message.send",
                    "hub.message.inbox", "hub.message.ack"} == set(names)
            result = await codex.call_tool("hub.health")
            assert result.structured_content["agentId"] == "codex"
            result = await codex.call_tool("unity.scenes.list")
            assert result.structured_content["data"]["scenes"] == ["Assets/Sample.unity"]
            created = (await codex.call_tool("hub.task.create", {
                "assignee": "claude", "request": "Review the scene",
                "idempotency_key": "task-1",
            })).structured_content["data"]
            task_id = created["task_id"]
            denied = (await codex.call_tool("hub.task.claim", {
                "task_id": task_id})).structured_content
            assert denied["error"]["code"] == "task_not_found"
            message = (await codex.call_tool("hub.message.send", {
                "recipient": "claude", "body": "Please review",
                "idempotency_key": "message-1", "task_id": task_id,
            })).structured_content["data"]

        async with Client(f"http://127.0.0.1:{ports[1]}/mcp", auth=tokens[1]) as claude:
            result = await claude.call_tool("hub.health")
            assert result.structured_content["agentId"] == "claude"
            inbox = (await claude.call_tool("hub.task.inbox")).structured_content["data"]
            assert [task["task_id"] for task in inbox] == [task_id]
            claimed = (await claude.call_tool("hub.task.claim", {
                "task_id": task_id})).structured_content["data"]
            assert claimed["state"] == "running"
            completed = (await claude.call_tool("hub.task.complete", {
                "task_id": task_id, "result": "Looks good",
            })).structured_content["data"]
            assert completed["state"] == "completed"
            messages = (await claude.call_tool("hub.message.inbox")).structured_content["data"]
            assert [item["message_id"] for item in messages] == [message["message_id"]]
            await claude.call_tool("hub.message.ack", {
                "message_id": message["message_id"]})
            assert (await claude.call_tool("hub.message.inbox")).structured_content["data"] == []

        async with Client(f"http://127.0.0.1:{ports[0]}/mcp", auth=tokens[0]) as codex:
            task = (await codex.call_tool("hub.task.get", {
                "task_id": task_id})).structured_content["data"]
            assert task["result"] == "Looks good"

        with pytest.raises(Exception):
            async with Client(f"http://127.0.0.1:{ports[1]}/mcp", auth=tokens[0]) as wrong:
                await wrong.list_tools()
        assert bridge.scene_calls == [None]
    finally:
        for server in servers:
            server.should_exit = True
        await asyncio.gather(*tasks)
        store.close()


def test_config_rejects_shared_tokens_and_non_loopback_bridge() -> None:
    shared = (AgentSpec("codex", 4317, "a" * 40), AgentSpec("claude", 4318, "a" * 40))
    with pytest.raises(ValueError, match="differ"):
        HubConfig("http://127.0.0.1:8080", shared).validate()
    distinct = (AgentSpec("codex", 4317, "a" * 40), AgentSpec("claude", 4318, "b" * 40))
    with pytest.raises(ValueError, match="loopback"):
        HubConfig("http://example.com:8080", distinct).validate()


@pytest.mark.asyncio
async def test_bridge_port_does_not_expose_legacy_mcp_or_unauthenticated_commands() -> None:
    async def inner(scope, receive, send):
        await PlainTextResponse("reached")(scope, receive, send)

    app = BridgeOnlyEndpoint(inner, token="private-bridge-token", port=48080)
    async with AsyncClient(transport=ASGITransport(app),
                           base_url="http://127.0.0.1:48080") as client:
        assert (await client.post("/mcp")).status_code == 404
        assert (await client.get("/api/instances")).status_code == 401
        allowed = await client.post("/api/command", headers={
            "X-Unity-Hub-Bridge-Token": "private-bridge-token"})
        assert allowed.status_code == 200
        assert allowed.text == "reached"
        blocked = await client.post("/api/command", headers={
            "X-Unity-Hub-Bridge-Token": "private-bridge-token",
            "Origin": "https://attacker.example"})
        assert blocked.status_code == 403


@pytest.mark.asyncio
async def test_editor_dashboard_exposes_metadata_without_task_bodies() -> None:
    store = HubStore(":memory:")
    task = store.create_task("codex", "claude", "private task body", "dash-task", "corr")
    store.send_message("codex", "claude", "private message body", "dash-message",
                       "corr-2", task["task_id"])
    dashboard = HubDashboard(store, {"codex": 4317, "claude": 4318})
    dashboard.record_agent_request("codex")

    async def inner(scope, receive, send):
        await PlainTextResponse("legacy")(scope, receive, send)

    app = BridgeOnlyEndpoint(inner, token="private", port=48081,
                             dashboard=dashboard.snapshot)
    async with AsyncClient(transport=ASGITransport(app),
                           base_url="http://127.0.0.1:48081") as client:
        response = await client.get("/hub/status")
        assert response.status_code == 200
        payload = response.json()
        assert payload["tasks"][0]["task_id"] == task["task_id"]
        assert payload["taskCounts"] == {"queued": 1}
        assert payload["unreadMessages"] == {"claude": 1}
        assert payload["agents"][0]["lastSeenAt"] is not None
        assert "private task body" not in response.text
        assert "private message body" not in response.text
        assert (await client.post("/hub/status")).status_code == 405
        assert (await client.get("/hub/status", headers={
            "Origin": "https://attacker.example"})).status_code == 403
    store.close()


@pytest.mark.asyncio
async def test_read_only_bridge_command_targets_the_selected_editor() -> None:
    calls = []

    def handler(request):
        calls.append(request)
        assert request.headers["X-Unity-Hub-Bridge-Token"] == "internal-token"
        if request.url.path == "/api/instances":
            return Response(200, json={"success": True, "instances": [
                {"project": "One", "hash": "one"},
                {"project": "Two", "hash": "two"},
            ]})
        assert request.url.path == "/api/command"
        assert json.loads(request.read()) == {
            "type": "manage_scene",
            "params": {"action": "get_loaded_scenes"},
            "unity_instance": "two",
        }
        return Response(200, json={"success": True, "data": {"scenes": ["Two.unity"]}})

    bridge = UnityBridge("http://127.0.0.1:8080", token="internal-token",
                         transport=MockTransport(handler))
    with pytest.raises(BridgeError, match="Specify unity_instance"):
        await bridge.loaded_scenes()
    assert (await bridge.loaded_scenes("Two"))["data"]["scenes"] == ["Two.unity"]
    assert [request.url.path for request in calls] == [
        "/api/instances", "/api/instances", "/api/command"]


def test_store_survives_restart_and_reuses_idempotency_key(tmp_path) -> None:
    path = tmp_path / "hub.sqlite3"
    store = HubStore(path)
    first = store.create_task("codex", "claude", "Review", "request-1", "corr-1")
    same = store.create_task("codex", "claude", "Review", "request-1", "corr-2")
    assert first["task_id"] == same["task_id"]
    assert store.connection.execute("SELECT COUNT(*) FROM audit_events").fetchone()[0] == 1
    store.close()

    reopened = HubStore(path)
    assert reopened.task_inbox("claude")[0]["task_id"] == first["task_id"]
    claimed = reopened.claim_task("claude", first["task_id"], "corr-3")
    assert claimed["state"] == "running"
    completed = reopened.complete_task("claude", first["task_id"], "Done", "corr-4")
    repeated = reopened.complete_task("claude", first["task_id"], "Done", "corr-5")
    assert repeated == completed
    with pytest.raises(Exception, match="different task"):
        reopened.create_task("codex", "claude", "Changed", "request-1", "corr-6")
    pending = reopened.create_task("codex", "claude", "Another", "request-2", "corr-7")
    assert reopened.cancel_task("codex", pending["task_id"], "corr-8")["state"] == "cancelled"
    with pytest.raises(Exception, match="cannot be claimed"):
        reopened.claim_task("claude", pending["task_id"], "corr-9")
    sent = reopened.send_message("codex", "claude", "Review done", "message-1",
                                 "corr-10", first["task_id"])
    duplicate = reopened.send_message("codex", "claude", "Review done", "message-1",
                                      "corr-11", first["task_id"])
    assert sent["message_id"] == duplicate["message_id"]
    with pytest.raises(Exception, match="not addressed"):
        reopened.acknowledge_message("codex", sent["message_id"], "corr-12")
    reopened.close()
