"""Exercise the real MCP transport and ownership rules without starting AI models."""
import json
import asyncio
import sys
import tempfile
import unittest
from contextlib import AsyncExitStack
from functools import wraps
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

import httpx
from hub.bridge import UnityBridge
from hub.config import AgentSpec, HubConfig
from hub.endpoint import create_endpoint
from hub.__main__ import StudioDashboard
from hub.store import HubStore


def with_endpoints(test):
    @wraps(test)
    async def wrapped(self):
        try:
            await self.open_endpoints()
            await test(self)
        finally:
            await self.close_endpoints()
    return wrapped


class StudioHubTests(unittest.IsolatedAsyncioTestCase):
    async def open_endpoints(self):
        self.stack = AsyncExitStack()
        self.temp = tempfile.TemporaryDirectory()
        self.store = HubStore(str(Path(self.temp.name) / "tasks.sqlite3"), {"primary", "worker", "reviewer"})
        self.clients = {}
        self.counter = 0
        for index, agent in enumerate(("primary", "worker", "reviewer")):
            token = agent.ljust(48, "x")
            endpoint = create_endpoint(agent, token, 4317 + index, UnityBridge("http://127.0.0.1:8080"), self.store, leader_id="primary")
            await self.stack.enter_async_context(endpoint.app.router.lifespan_context(endpoint.app))
            client = await self.stack.enter_async_context(httpx.AsyncClient(
                transport=httpx.ASGITransport(app=endpoint), base_url=f"http://127.0.0.1:{4317 + index}",
                headers={"Authorization": f"Bearer {token}", "Accept": "application/json, text/event-stream", "MCP-Protocol-Version": "2025-03-26"}))
            self.clients[agent] = client
            await self.rpc(agent, "initialize", {"protocolVersion": "2025-03-26", "capabilities": {},
                                                   "clientInfo": {"name": "studio-check", "version": "1"}})
            response = await client.post("/mcp", json={"jsonrpc": "2.0", "method": "notifications/initialized"})
            self.assertLess(response.status_code, 300)

    async def close_endpoints(self):
        await self.stack.aclose()
        self.store.close()
        self.temp.cleanup()

    async def rpc(self, agent, method, params):
        self.counter += 1
        response = await self.clients[agent].post("/mcp", json={"jsonrpc": "2.0", "id": self.counter, "method": method, "params": params})
        self.assertEqual(response.status_code, 200, response.text)
        body = response.text
        payload = json.loads(body) if body.lstrip().startswith("{") else next(
            json.loads(line[5:].strip()) for line in body.splitlines() if line.startswith("data:"))
        self.assertNotIn("error", payload, payload)
        return payload["result"]

    async def tool(self, agent, name, arguments=None):
        result = await self.rpc(agent, "tools/call", {"name": name, "arguments": arguments or {}})
        return result.get("structuredContent") or json.loads(result["content"][0]["text"])

    @with_endpoints
    async def test_primary_assigns_worker_completes_and_result_returns(self):
        team = await self.tool("primary", "hub.agents.list")
        self.assertEqual(team["data"]["leader"], "primary")
        task = await self.tool("primary", "hub.task.create", {"assignee": "worker", "request": "Review a temporary fixture", "idempotency_key": "goal-1"})
        task_id = task["data"]["task_id"]
        inbox = await self.tool("worker", "hub.task.inbox")
        self.assertEqual(inbox["data"][0]["task_id"], task_id)
        await self.tool("worker", "hub.task.claim", {"task_id": task_id})
        await self.tool("worker", "hub.task.complete", {"task_id": task_id, "result": "Fixture reviewed"})
        result = await self.tool("primary", "hub.task.get", {"task_id": task_id})
        self.assertEqual(result["data"]["state"], "completed")
        self.assertEqual(result["data"]["result"], "Fixture reviewed")
        self.assertEqual(self.store.dashboard_snapshot()["taskCounts"]["completed"], 1)
        self.assertEqual(len(self.store.transcript()["events"]), 3)

    @with_endpoints
    async def test_worker_cannot_assign_or_read_another_workers_task(self):
        denied = await self.tool("worker", "hub.task.create", {"assignee": "reviewer", "request": "Unallowed", "idempotency_key": "no"})
        self.assertEqual(denied["error"]["code"], "leader_required")
        task = await self.tool("primary", "hub.task.create", {"assignee": "worker", "request": "Private task", "idempotency_key": "private"})
        denied = await self.tool("reviewer", "hub.task.get", {"task_id": task["data"]["task_id"]})
        self.assertEqual(denied["status"], "failed")

    @with_endpoints
    async def test_retry_same_key_does_not_duplicate_and_survives_reopen(self):
        args = {"assignee": "worker", "request": "Stable request", "idempotency_key": "retry"}
        first = await self.tool("primary", "hub.task.create", args)
        second = await self.tool("primary", "hub.task.create", args)
        self.assertEqual(first["data"]["task_id"], second["data"]["task_id"])
        other = HubStore(str(Path(self.temp.name) / "tasks.sqlite3"), {"primary", "worker", "reviewer"})
        try:
            self.assertEqual(len(other.task_inbox("worker")), 1)
        finally:
            other.close()

    @with_endpoints
    async def test_auth_rejects_missing_token_and_wrong_host(self):
        client = self.clients["primary"]
        response = await client.post("/mcp", headers={"Authorization": ""}, json={})
        self.assertEqual(response.status_code, 401)
        response = await client.post("/mcp", headers={"Host": "other.example"}, json={})
        self.assertEqual(response.status_code, 403)

    @with_endpoints
    async def test_waiting_worker_wakes_when_primary_assigns(self):
        async def assign_later():
            await asyncio.sleep(0.05)
            return await self.tool("primary", "hub.task.create", {"assignee": "worker", "request": "Wake a waiting worker", "idempotency_key": "wake"})
        waiting, created = await asyncio.gather(
            self.tool("worker", "hub.task.wait", {"timeout_seconds": 2}), assign_later())
        self.assertEqual(waiting["data"][0]["task_id"], created["data"]["task_id"])
        events = await self.tool("primary", "hub.events.wait", {"after": 0, "timeout_seconds": 1})
        self.assertEqual(events["data"]["events"][0]["kind"], "task.created")

    @with_endpoints
    async def test_dashboard_has_no_tokens_and_rejects_foreign_origin(self):
        config = HubConfig("http://127.0.0.1:8080", (AgentSpec("primary", 4317, "a" * 48),))
        dashboard = StudioDashboard(config, self.store, "primary", 4320)
        async with httpx.AsyncClient(transport=httpx.ASGITransport(app=dashboard), base_url="http://127.0.0.1:4320") as client:
            response = await client.get("/hub/status")
            self.assertEqual(response.json()["product"], "novahn-studio")
            self.assertNotIn("token", response.text.lower())
            self.assertEqual((await client.get("/hub/status", headers={"Origin": "https://other.example"})).status_code, 403)


if __name__ == "__main__":
    unittest.main()
