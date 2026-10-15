"""Configuration and startup validation for the local hub."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path
from urllib.parse import urlparse


@dataclass(frozen=True)
class HubConfig:
    bridge_url: str
    codex_port: int
    claude_port: int
    codex_token: str
    claude_token: str
    db_path: str = str(Path.home() / ".novahn-unity-hub" / "hub.sqlite3")

    @classmethod
    def from_env(cls) -> "HubConfig":
        config = cls(
            bridge_url=os.environ.get("UNITY_HUB_BRIDGE_URL", "http://127.0.0.1:8080"),
            codex_port=int(os.environ.get("UNITY_HUB_CODEX_PORT", "4317")),
            claude_port=int(os.environ.get("UNITY_HUB_CLAUDE_PORT", "4318")),
            codex_token=os.environ.get("UNITY_HUB_CODEX_TOKEN", ""),
            claude_token=os.environ.get("UNITY_HUB_CLAUDE_TOKEN", ""),
            db_path=os.environ.get("UNITY_HUB_DB_PATH", str(
                Path.home() / ".novahn-unity-hub" / "hub.sqlite3")),
        )
        config.validate()
        return config

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
        ports = (self.codex_port, self.claude_port, bridge_port)
        if any(port < 1 or port > 65535 for port in ports) or len(set(ports)) != 3:
            raise ValueError("Codex, Claude and bridge ports must be distinct valid ports")
        if len(self.codex_token) < 32 or len(self.claude_token) < 32:
            raise ValueError("Set distinct UNITY_HUB_*_TOKEN values of at least 32 characters")
        if self.codex_token == self.claude_token:
            raise ValueError("Codex and Claude tokens must differ")
        if any(not token.isascii() or any(char.isspace() for char in token)
               for token in (self.codex_token, self.claude_token)):
            raise ValueError("Hub tokens must be ASCII without whitespace")
