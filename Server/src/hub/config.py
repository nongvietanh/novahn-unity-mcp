"""Configuration and startup validation for the local hub."""

from __future__ import annotations

import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from urllib.parse import urlparse

_DEFAULT_DB_PATH = str(Path.home() / ".novahn-unity-hub" / "hub.sqlite3")
_AGENT_ID = re.compile(r"^[a-z0-9_-]{1,32}$")


@dataclass(frozen=True)
class AgentSpec:
    """One externally connected AI: its id, loopback port and bearer token."""
    id: str
    port: int
    token: str


@dataclass(frozen=True)
class HubConfig:
    bridge_url: str
    agents: tuple[AgentSpec, ...] = field(default_factory=tuple)
    db_path: str = _DEFAULT_DB_PATH

    @classmethod
    def from_env(cls) -> "HubConfig":
        config = cls(
            bridge_url=os.environ.get("UNITY_HUB_BRIDGE_URL", "http://127.0.0.1:8080"),
            agents=cls._agents_from_env(),
            db_path=os.environ.get("UNITY_HUB_DB_PATH", _DEFAULT_DB_PATH),
        )
        config.validate()
        return config

    @staticmethod
    def _agents_from_env() -> tuple[AgentSpec, ...]:
        """Parse UNITY_HUB_AGENTS=id:port,id:port with UNITY_HUB_TOKEN_<ID> tokens.

        Falls back to the legacy codex/claude variables when UNITY_HUB_AGENTS is unset,
        so existing setups keep working unchanged.
        """
        spec = os.environ.get("UNITY_HUB_AGENTS", "").strip()
        if not spec:
            return (
                AgentSpec("codex", int(os.environ.get("UNITY_HUB_CODEX_PORT", "4317")),
                          os.environ.get("UNITY_HUB_CODEX_TOKEN", "")),
                AgentSpec("claude", int(os.environ.get("UNITY_HUB_CLAUDE_PORT", "4318")),
                          os.environ.get("UNITY_HUB_CLAUDE_TOKEN", "")),
            )
        agents: list[AgentSpec] = []
        for entry in spec.split(","):
            entry = entry.strip()
            if not entry:
                continue
            agent_id, _, port_text = entry.partition(":")
            agent_id = agent_id.strip()
            try:
                port = int(port_text.strip())
            except ValueError as exc:
                raise ValueError(f"UNITY_HUB_AGENTS entry '{entry}' must be id:port") from exc
            env_key = "UNITY_HUB_TOKEN_" + agent_id.upper().replace("-", "_")
            agents.append(AgentSpec(agent_id, port, os.environ.get(env_key, "")))
        return tuple(agents)

    def validate(self) -> None:
        parsed = urlparse(self.bridge_url)
        if (parsed.scheme != "http" or parsed.hostname not in {"127.0.0.1", "localhost"}
                or parsed.username or parsed.password or parsed.path not in {"", "/"}
                or parsed.query or parsed.fragment):
            raise ValueError("UNITY_HUB_BRIDGE_URL must be a loopback HTTP origin")
        try:
            bridge_port = parsed.port or 80
        except ValueError as exc:
            raise ValueError("UNITY_HUB_BRIDGE_URL has an invalid port") from exc

        if not self.agents:
            raise ValueError("Configure at least one agent (UNITY_HUB_AGENTS)")

        ids = [agent.id for agent in self.agents]
        if any(not _AGENT_ID.match(agent_id) for agent_id in ids):
            raise ValueError("Agent ids must match [a-z0-9_-]{1,32}")
        if len(set(ids)) != len(ids):
            raise ValueError("Agent ids must be distinct")

        ports = [bridge_port] + [agent.port for agent in self.agents]
        if any(port < 1 or port > 65535 for port in ports) or len(set(ports)) != len(ports):
            raise ValueError("Agent and bridge ports must be distinct valid ports")

        tokens = [agent.token for agent in self.agents]
        if any(len(token) < 32 for token in tokens):
            raise ValueError("Set UNITY_HUB_TOKEN_<ID> values of at least 32 characters per agent")
        if len(set(tokens)) != len(tokens):
            raise ValueError("Each agent token must differ")
        if any(not token.isascii() or any(char.isspace() for char in token) for token in tokens):
            raise ValueError("Hub tokens must be ASCII without whitespace")
