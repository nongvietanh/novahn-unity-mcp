"""Metadata-only snapshot consumed by the Unity Editor Hub window."""

from __future__ import annotations

import time
from datetime import datetime, timezone
from typing import Any

from hub.store import HubStore


class HubDashboard:
    def __init__(self, store: HubStore, ports: dict[str, int]):
        self.store = store
        self.ports = dict(ports)
        self.last_seen: dict[str, float | None] = {agent_id: None for agent_id in ports}

    def record_agent_request(self, agent_id: str) -> None:
        if agent_id in self.last_seen:
            self.last_seen[agent_id] = time.time()

    async def snapshot(self) -> dict[str, Any]:
        from transport.plugin_hub import PluginHub

        sessions = await PluginHub.get_sessions()
        instances = [
            {"project": session.project, "hash": session.hash,
             "unityVersion": session.unity_version,
             "connectedAt": session.connected_at}
            for session in sessions.sessions.values()
        ]
        instances.sort(key=lambda item: (item["project"], item["hash"]))
        agents = [
            {"agentId": agent_id, "port": port,
             "lastSeenAt": self.last_seen[agent_id]}
            for agent_id, port in self.ports.items()
        ]
        return {
            "status": "ready",
            "updatedAt": datetime.now(timezone.utc).isoformat(),
            "agents": agents,
            "instances": instances,
            **self.store.dashboard_snapshot(),
        }
