"""Start the two local MCP endpoints: python -m hub."""

from __future__ import annotations

import asyncio
import logging
import os
import secrets
import socket
from urllib.parse import urlparse

import uvicorn

from hub.bridge import UnityBridge
from hub.bridge_endpoint import BridgeOnlyEndpoint
from hub.config import HubConfig
from hub.dashboard import HubDashboard
from hub.endpoint import create_endpoint
from hub.store import HubStore


async def serve(config: HubConfig) -> None:
    # Keep the existing Unity WebSocket bridge but remove its direct MCP surface.
    os.environ.setdefault("UNITY_MCP_DISABLE_TELEMETRY", "1")
    from core.config import config as legacy_config
    from main import create_mcp_server

    legacy_config.transport_mode = "http"
    legacy_config.http_remote_hosted = False
    legacy_config.telemetry_enabled = False
    bridge_port = urlparse(config.bridge_url).port or 80
    bridge_token = secrets.token_urlsafe(48)
    bridge_mcp = create_mcp_server(project_scoped_tools=False)
    store = HubStore(config.db_path, {agent.id for agent in config.agents})
    dashboard = HubDashboard(store, {agent.id: agent.port for agent in config.agents})

    async def transcript(after: int, limit: int) -> dict:
        data = store.transcript(after, limit)
        data["agents"] = list(dashboard.ports)
        return data

    bridge_app = BridgeOnlyEndpoint(
        bridge_mcp.http_app(path="/mcp", stateless_http=True),
        token=bridge_token, port=bridge_port, dashboard=dashboard.snapshot,
        transcript=transcript,
    )
    bridge = UnityBridge(config.bridge_url, token=bridge_token)
    servers = []
    sockets = []
    # Order is fixed so sockets and servers stay aligned for zip() below.
    endpoints = [("bridge", bridge_port, None)] + [
        (agent.id, agent.port, agent.token) for agent in config.agents]
    ports = tuple(port for _, port, _ in endpoints)
    try:
        for port in ports:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sockets.append(sock)
            sock.bind(("127.0.0.1", port))
            sock.listen(128)
    except OSError as exc:
        for sock in sockets:
            sock.close()
        store.close()
        raise RuntimeError(f"Cannot reserve Hub ports {ports}: {exc}") from exc

    for agent_id, port, token in endpoints:
        app = (bridge_app if agent_id == "bridge" else
               create_endpoint(agent_id, token, port, bridge, store,
                               on_authenticated=lambda agent=agent_id:
                               dashboard.record_agent_request(agent)))
        server = uvicorn.Server(uvicorn.Config(
            app, host="127.0.0.1", port=port, log_level="info",
            access_log=False, lifespan="on",
        ))
        server.install_signal_handlers = lambda: None
        servers.append(server)
    try:
        await asyncio.gather(*(server.serve(sockets=[sock])
                               for server, sock in zip(servers, sockets)))
    finally:
        for server in servers:
            server.should_exit = True
        for sock in sockets:
            sock.close()
        store.close()


def main() -> None:
    logging.basicConfig(level=logging.INFO)
    config = HubConfig.from_env()
    try:
        asyncio.run(serve(config))
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
