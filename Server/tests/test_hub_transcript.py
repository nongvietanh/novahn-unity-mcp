"""The AI Console transcript exposes task/message bodies in timeline order."""

from __future__ import annotations

import pytest
from httpx import ASGITransport, AsyncClient
from starlette.responses import PlainTextResponse

from hub.bridge_endpoint import BridgeOnlyEndpoint
from hub.store import HubStore


def _seed(store: HubStore) -> str:
    task = store.create_task("codex", "claude", "Create Player prefab", "k-task", "c1")
    task_id = task["task_id"]
    store.claim_task("claude", task_id, "c2")
    store.send_message("claude", "codex", "Need collider review", "k-msg", "c3", task_id)
    store.complete_task("claude", task_id, "Prefab saved at Assets/Player.prefab", "c4")
    return task_id


def test_transcript_returns_bodies_in_order() -> None:
    store = HubStore(":memory:")
    task_id = _seed(store)

    result = store.transcript()
    kinds = [event["kind"] for event in result["events"]]
    assert kinds == ["task.created", "task.claimed", "message.sent", "task.completed"]

    created, claimed, message, completed = result["events"]
    assert created["actor"] == "codex" and created["assignee"] == "claude"
    assert created["text"] == "Create Player prefab"
    # State is joined live, so every task event reflects the task's current state.
    assert claimed["actor"] == "claude" and claimed["state"] == "completed"
    assert message["kind"] == "message.sent"
    assert message["sender"] == "claude" and message["recipient"] == "codex"
    assert message["text"] == "Need collider review" and message["taskId"] == task_id
    # task.completed surfaces the result body, not the original request.
    assert completed["text"] == "Prefab saved at Assets/Player.prefab"
    assert completed["state"] == "completed"
    # The cursor advances monotonically and equals the last event's seq.
    assert result["nextCursor"] == completed["seq"]
    assert [event["seq"] for event in result["events"]] == sorted(
        event["seq"] for event in result["events"])
    store.close()


def test_transcript_is_incremental_by_cursor() -> None:
    store = HubStore(":memory:")
    _seed(store)
    first = store.transcript(limit=2)
    assert len(first["events"]) == 2

    rest = store.transcript(after_seq=first["nextCursor"])
    assert [event["kind"] for event in rest["events"]] == ["message.sent", "task.completed"]
    assert rest["nextCursor"] > first["nextCursor"]
    # Polling again past the end yields nothing and keeps the cursor stable.
    empty = store.transcript(after_seq=rest["nextCursor"])
    assert empty["events"] == []
    assert empty["nextCursor"] == rest["nextCursor"]
    store.close()


def test_transcript_clips_large_bodies() -> None:
    store = HubStore(":memory:")
    store.create_task("codex", "claude", "x" * 5000, "k-big", "c1")
    event = store.transcript(snippet_limit=100)["events"][0]
    assert len(event["text"]) == 101 and event["text"].endswith("…")
    store.close()


def test_transcript_clamps_limit() -> None:
    store = HubStore(":memory:")
    assert store.transcript(limit=9999, after_seq=-5)["events"] == []
    store.close()


@pytest.mark.asyncio
async def test_transcript_route_serves_events_over_loopback() -> None:
    store = HubStore(":memory:")
    _seed(store)

    async def transcript(after: int, limit: int) -> dict:
        data = store.transcript(after, limit)
        data["agents"] = ["codex", "claude"]
        return data

    async def inner(scope, receive, send):
        await PlainTextResponse("legacy")(scope, receive, send)

    app = BridgeOnlyEndpoint(inner, token="private", port=48091,
                             dashboard=None, transcript=transcript)
    async with AsyncClient(transport=ASGITransport(app),
                           base_url="http://127.0.0.1:48091") as client:
        response = await client.get("/hub/transcript")
        assert response.status_code == 200
        payload = response.json()
        assert payload["agents"] == ["codex", "claude"]
        assert payload["events"][0]["text"] == "Create Player prefab"

        cursor = payload["events"][1]["seq"]
        tail = (await client.get(f"/hub/transcript?after={cursor}&limit=50")).json()
        assert [event["kind"] for event in tail["events"]] == [
            "message.sent", "task.completed"]

        assert (await client.post("/hub/transcript")).status_code == 405
        assert (await client.get("/hub/transcript", headers={
            "Origin": "https://attacker.example"})).status_code == 403
    store.close()
