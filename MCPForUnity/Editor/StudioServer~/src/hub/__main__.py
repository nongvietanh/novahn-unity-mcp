"""Run task coordination beside the project's existing Unity MCP server."""
from __future__ import annotations

import asyncio
import json
import os
import socket
import time
from typing import Any

import uvicorn
from starlette.responses import JSONResponse, PlainTextResponse

from hub.bridge import BridgeError, UnityBridge
from hub.config import HubConfig
from hub.endpoint import create_endpoint
from hub.store import HubStore


class StudioDashboard:
    def __init__(self, config: HubConfig, store: HubStore, leader: str, port: int):
        self.config, self.store, self.leader, self.port = config, store, leader, port
        self.last_seen: dict[str, float | None] = {a.id: None for a in config.agents}
        self.instance_id = os.environ.get("UNITY_HUB_INSTANCE_ID", "")
        self.bridge = UnityBridge(config.bridge_url)

    def record(self, agent_id: str) -> None:
        self.last_seen[agent_id] = time.time()

    async def __call__(self, scope: dict, receive: Any, send: Any) -> None:
        if scope["type"] == "lifespan":
            while True:
                message = await receive()
                if message["type"] == "lifespan.startup":
                    await send({"type": "lifespan.startup.complete"})
                elif message["type"] == "lifespan.shutdown":
                    await send({"type": "lifespan.shutdown.complete"})
                    return
        if scope["type"] != "http":
            return
        headers = {k.lower(): v for k, v in scope.get("headers", [])}
        hosts = {f"127.0.0.1:{self.port}", f"localhost:{self.port}"}
        host = headers.get(b"host", b"").decode("ascii", errors="ignore")
        origin = headers.get(b"origin", b"").decode("ascii", errors="ignore")
        if host not in hosts or (origin and origin not in {f"http://{h}" for h in hosts}):
            await PlainTextResponse("Forbidden", status_code=403)(scope, receive, send)
            return
        if scope.get("method") != "GET":
            await PlainTextResponse("Method not allowed", status_code=405)(scope, receive, send)
            return
        path = scope.get("path", "")
        if path in {"/health", "/hub/status"}:
            payload = {
                "status": "ready", "product": "novahn-studio", "instanceId": self.instance_id,
                "leader": self.leader,
                "agents": [{"agentId": a.id, "port": a.port,
                            "role": "leader" if a.id == self.leader else "worker",
                            "lastSeenAt": self.last_seen[a.id]} for a in self.config.agents],
                **self.store.dashboard_snapshot(),
            }
        elif path == "/hub/transcript":
            from urllib.parse import parse_qs
            query = parse_qs(scope.get("query_string", b"").decode("ascii", errors="ignore"))
            try:
                after, limit = int(query.get("after", ["0"])[0]), int(query.get("limit", ["100"])[0])
            except (ValueError, IndexError):
                after, limit = 0, 100
            payload = {**self.store.transcript(after, limit), "agents": [a.id for a in self.config.agents]}
        else:
            await PlainTextResponse("Not found", status_code=404)(scope, receive, send)
            return
        await JSONResponse(payload)(scope, receive, send)


async def serve(config: HubConfig) -> None:
    leader = os.environ.get("UNITY_HUB_LEADER", config.agents[0].id)
    if leader not in {a.id for a in config.agents}:
        raise ValueError("The primary AI must belong to this team")
    dashboard_port = int(os.environ.get("UNITY_HUB_DASHBOARD_PORT", "4320"))
    ports = [dashboard_port] + [a.port for a in config.agents]
    if len(set(ports)) != len(ports) or any(p < 1024 or p > 65535 for p in ports):
        raise ValueError("Use distinct ports between 1024 and 65535")
    store = HubStore(config.db_path, {a.id for a in config.agents})
    dashboard = StudioDashboard(config, store, leader, dashboard_port)
    apps = [dashboard] + [create_endpoint(a.id, a.token, a.port, dashboard.bridge, store,
                                         on_authenticated=lambda agent=a.id: dashboard.record(agent),
                                         leader_id=leader) for a in config.agents]
    sockets, servers = [], []
    try:
        # Reserve every port first, so a conflict cannot leave a partially started team.
        for port in ports:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sockets.append(sock)
            sock.bind(("127.0.0.1", port))
            sock.listen(128)
        for app in apps:
            server = uvicorn.Server(uvicorn.Config(app, host="127.0.0.1", access_log=False,
                                                   log_level="warning", lifespan="on"))
            server.install_signal_handlers = lambda: None
            servers.append(server)
        await asyncio.gather(*(server.serve(sockets=[sock]) for server, sock in zip(servers, sockets)))
    finally:
        for server in servers:
            server.should_exit = True
        for sock in sockets:
            sock.close()
        store.close()


def main() -> None:
    try:
        asyncio.run(serve(HubConfig.from_env()))
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
