"""Read-only adapter to the existing local Unity MCP bridge."""

from __future__ import annotations

from typing import Any

import httpx


class BridgeError(RuntimeError):
    def __init__(self, code: str, message: str):
        super().__init__(message)
        self.code = code


class UnityBridge:
    def __init__(self, base_url: str, *, token: str = "", timeout: float = 15.0,
                 transport: httpx.AsyncBaseTransport | None = None):
        self.base_url = base_url.rstrip("/")
        self.token = token
        self.timeout = timeout
        self.transport = transport

    async def _request(self, method: str, path: str, **kwargs: Any) -> dict[str, Any]:
        try:
            async with httpx.AsyncClient(timeout=self.timeout, trust_env=False,
                                         transport=self.transport) as client:
                response = await client.request(
                    method, f"{self.base_url}{path}",
                    headers={"X-Unity-Hub-Bridge-Token": self.token}, **kwargs)
                response.raise_for_status()
                payload = response.json()
        except (httpx.HTTPError, ValueError) as exc:
            raise BridgeError("bridge_unavailable", "Unity MCP bridge is unavailable") from exc
        if not isinstance(payload, dict):
            raise BridgeError("invalid_bridge_response", "Unity MCP bridge returned invalid data")
        return payload

    async def instances(self) -> list[dict[str, Any]]:
        payload = await self._request("GET", "/api/instances")
        if not payload.get("success") or not isinstance(payload.get("instances"), list):
            raise BridgeError("bridge_unavailable", "Unity MCP bridge cannot list instances")
        return payload["instances"]

    async def loaded_scenes(self, unity_instance: str | None = None) -> dict[str, Any]:
        instances = await self.instances()
        if not instances:
            raise BridgeError("unity_offline", "No Unity Editor is connected")
        if unity_instance:
            matches = [item for item in instances if unity_instance in {
                item.get("hash"), item.get("project"),
                f"{item.get('project')}@{item.get('hash')}",
            }]
            if len(matches) != 1:
                raise BridgeError("instance_not_found", "Unity instance is missing or ambiguous")
            instance = matches[0]
        elif len(instances) == 1:
            instance = instances[0]
        else:
            raise BridgeError("instance_required", "Specify unity_instance when multiple Editors are connected")

        result = await self._request("POST", "/api/command", json={
            "type": "manage_scene",
            "params": {"action": "get_loaded_scenes"},
            "unity_instance": instance["hash"],
        })
        if not result.get("success"):
            raise BridgeError("unity_command_failed", str(result.get("error") or result.get("message") or "Unity command failed"))
        return result
