"""The hub accepts an arbitrary set of agents, not just codex/claude."""

from __future__ import annotations

import pytest

from hub.config import AgentSpec, HubConfig
from hub.store import HubStore, StoreError

_TOKEN = "z" * 40


def test_from_env_parses_agent_list(monkeypatch) -> None:
    monkeypatch.delenv("UNITY_HUB_CODEX_PORT", raising=False)
    monkeypatch.setenv("UNITY_HUB_AGENTS", "codex:4317, claude:4318 , gemini:4319")
    monkeypatch.setenv("UNITY_HUB_TOKEN_CODEX", "a" * 40)
    monkeypatch.setenv("UNITY_HUB_TOKEN_CLAUDE", "b" * 40)
    monkeypatch.setenv("UNITY_HUB_TOKEN_GEMINI", "c" * 40)

    config = HubConfig.from_env()
    assert [agent.id for agent in config.agents] == ["codex", "claude", "gemini"]
    assert [agent.port for agent in config.agents] == [4317, 4318, 4319]
    assert config.agents[2].token == "c" * 40


def test_from_env_falls_back_to_legacy_two_agents(monkeypatch) -> None:
    monkeypatch.delenv("UNITY_HUB_AGENTS", raising=False)
    monkeypatch.setenv("UNITY_HUB_CODEX_TOKEN", "a" * 40)
    monkeypatch.setenv("UNITY_HUB_CLAUDE_TOKEN", "b" * 40)

    config = HubConfig.from_env()
    assert [agent.id for agent in config.agents] == ["codex", "claude"]
    assert [agent.port for agent in config.agents] == [4317, 4318]


def test_validate_rejects_bad_configurations() -> None:
    bridge = "http://127.0.0.1:8080"
    with pytest.raises(ValueError, match="distinct valid ports"):
        HubConfig(bridge, (AgentSpec("a", 5000, _TOKEN),
                           AgentSpec("b", 5000, "y" * 40))).validate()
    with pytest.raises(ValueError, match="distinct valid ports"):
        # Collides with the bridge port 8080.
        HubConfig(bridge, (AgentSpec("a", 8080, _TOKEN),)).validate()
    with pytest.raises(ValueError, match="Agent ids"):
        HubConfig(bridge, (AgentSpec("Bad Id", 5000, _TOKEN),)).validate()
    with pytest.raises(ValueError, match="distinct"):
        HubConfig(bridge, (AgentSpec("dup", 5000, _TOKEN),
                           AgentSpec("dup", 5001, "y" * 40))).validate()
    with pytest.raises(ValueError, match="32 characters"):
        HubConfig(bridge, (AgentSpec("a", 5000, "short"),)).validate()
    with pytest.raises(ValueError, match="at least one agent"):
        HubConfig(bridge, ()).validate()


def test_validate_accepts_three_agents() -> None:
    HubConfig("http://127.0.0.1:8080", (
        AgentSpec("codex", 4317, "a" * 40),
        AgentSpec("claude", 4318, "b" * 40),
        AgentSpec("gemini", 4319, "c" * 40),
    )).validate()


def test_store_routes_tasks_to_any_configured_agent() -> None:
    store = HubStore(":memory:", {"codex", "claude", "gemini"})
    task = store.create_task("codex", "gemini", "Bake lightmaps", "k1", "c1")
    assert task["assignee"] == "gemini"
    store.send_message("gemini", "claude", "Lightmaps queued", "k2", "c2")

    with pytest.raises(StoreError, match="Unknown agent"):
        store.create_task("codex", "nobody", "x", "k3", "c3")
    store.close()
