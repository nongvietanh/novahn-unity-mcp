"""Durable local task and message inbox for externally connected agents."""

from __future__ import annotations

import sqlite3
import time
import uuid
from pathlib import Path
from typing import Any


_DEFAULT_AGENTS = frozenset({"codex", "claude"})


class StoreError(ValueError):
    def __init__(self, code: str, message: str):
        super().__init__(message)
        self.code = code


def _utc_now() -> float:
    return time.time()


def _require_text(value: str, name: str, limit: int) -> None:
    if not isinstance(value, str) or not value.strip() or len(value) > limit:
        raise StoreError("invalid_input", f"{name} must contain 1–{limit} characters")


def _clip(value: str | None, limit: int) -> str:
    if not value:
        return ""
    return value if len(value) <= limit else value[:limit] + "…"


class HubStore:
    def __init__(self, path: str | Path, agents: set[str] | None = None):
        self.agents = set(agents) if agents else set(_DEFAULT_AGENTS)
        if str(path) != ":memory:":
            Path(path).parent.mkdir(parents=True, exist_ok=True)
        self.connection = sqlite3.connect(str(path))
        self.connection.row_factory = sqlite3.Row
        self.connection.execute("PRAGMA foreign_keys=ON")
        if str(path) != ":memory:":
            self.connection.execute("PRAGMA journal_mode=WAL")
        self.connection.executescript("""
            CREATE TABLE IF NOT EXISTS tasks (
                task_id TEXT PRIMARY KEY,
                creator TEXT NOT NULL,
                assignee TEXT NOT NULL,
                request TEXT NOT NULL,
                result TEXT,
                state TEXT NOT NULL,
                idempotency_key TEXT NOT NULL,
                created_at REAL NOT NULL,
                updated_at REAL NOT NULL,
                UNIQUE(creator, idempotency_key)
            );
            CREATE TABLE IF NOT EXISTS messages (
                message_id TEXT PRIMARY KEY,
                task_id TEXT REFERENCES tasks(task_id),
                sender TEXT NOT NULL,
                recipient TEXT NOT NULL,
                body TEXT NOT NULL,
                state TEXT NOT NULL,
                idempotency_key TEXT NOT NULL,
                created_at REAL NOT NULL,
                acknowledged_at REAL,
                UNIQUE(sender, idempotency_key)
            );
            CREATE TABLE IF NOT EXISTS audit_events (
                event_id TEXT PRIMARY KEY,
                correlation_id TEXT NOT NULL,
                actor TEXT NOT NULL,
                kind TEXT NOT NULL,
                object_id TEXT NOT NULL,
                created_at REAL NOT NULL
            );
        """)

    def close(self) -> None:
        self.connection.close()

    def dashboard_snapshot(self) -> dict[str, Any]:
        """Return metadata for the local Editor window, never task/message bodies."""
        task_counts = {row["state"]: row["count"] for row in self.connection.execute(
            "SELECT state, COUNT(*) AS count FROM tasks GROUP BY state")}
        unread_counts = {row["recipient"]: row["count"] for row in self.connection.execute(
            "SELECT recipient, COUNT(*) AS count FROM messages "
            "WHERE state='unread' GROUP BY recipient")}
        tasks = [dict(row) for row in self.connection.execute(
            "SELECT task_id, creator, assignee, state, created_at, updated_at "
            "FROM tasks ORDER BY updated_at DESC, task_id DESC LIMIT 20")]
        return {
            "taskCounts": task_counts,
            "unreadMessages": unread_counts,
            "tasks": tasks,
        }

    def transcript(self, after_seq: int = 0, limit: int = 200,
                   snippet_limit: int = 2000) -> dict[str, Any]:
        """Return the agent-to-agent timeline with bodies, for the AI Console view.

        Uses the audit_events rowid as a monotonic cursor so the Editor can poll
        incrementally: pass the previous nextCursor as after_seq to fetch only new
        events. Bodies are clipped to snippet_limit so large payloads never flood
        the console.
        """
        limit = max(1, min(int(limit), 500))
        after_seq = max(0, int(after_seq))
        rows = self.connection.execute(
            "SELECT rowid AS seq, actor, kind, object_id, created_at "
            "FROM audit_events WHERE rowid > ? ORDER BY rowid LIMIT ?",
            (after_seq, limit)).fetchall()
        events: list[dict[str, Any]] = []
        next_cursor = after_seq
        for row in rows:
            next_cursor = row["seq"]
            event: dict[str, Any] = {
                "seq": row["seq"], "at": row["created_at"],
                "actor": row["actor"], "kind": row["kind"],
            }
            if row["kind"].startswith("task."):
                task = self.connection.execute(
                    "SELECT creator, assignee, request, result, state "
                    "FROM tasks WHERE task_id=?", (row["object_id"],)).fetchone()
                if task is not None:
                    body = task["result"] if row["kind"] == "task.completed" else task["request"]
                    event.update(taskId=row["object_id"], creator=task["creator"],
                                 assignee=task["assignee"], state=task["state"],
                                 text=_clip(body, snippet_limit))
            elif row["kind"].startswith("message."):
                message = self.connection.execute(
                    "SELECT sender, recipient, body, task_id "
                    "FROM messages WHERE message_id=?", (row["object_id"],)).fetchone()
                if message is not None:
                    event.update(messageId=row["object_id"], sender=message["sender"],
                                 recipient=message["recipient"], taskId=message["task_id"],
                                 text=_clip(message["body"], snippet_limit))
            events.append(event)
        return {"events": events, "nextCursor": next_cursor}

    def _audit(self, correlation_id: str, actor: str, kind: str,
               object_id: str, now: float) -> None:
        self.connection.execute(
            "INSERT INTO audit_events VALUES (?, ?, ?, ?, ?, ?)",
            (str(uuid.uuid4()), correlation_id, actor, kind, object_id, now))

    @staticmethod
    def _task(row: sqlite3.Row) -> dict[str, Any]:
        return {key: row[key] for key in (
            "task_id", "creator", "assignee", "request", "result", "state",
            "created_at", "updated_at")}

    @staticmethod
    def _message(row: sqlite3.Row) -> dict[str, Any]:
        return {key: row[key] for key in (
            "message_id", "task_id", "sender", "recipient", "body", "state",
            "created_at", "acknowledged_at")}

    def create_task(self, creator: str, assignee: str, request: str,
                    idempotency_key: str, correlation_id: str) -> dict[str, Any]:
        if assignee not in self.agents:
            raise StoreError("invalid_assignee", "Unknown agent")
        _require_text(request, "request", 10000)
        _require_text(idempotency_key, "idempotency_key", 128)
        now = _utc_now()
        with self.connection:
            row = self.connection.execute(
                "SELECT * FROM tasks WHERE creator=? AND idempotency_key=?",
                (creator, idempotency_key)).fetchone()
            if row:
                if row["assignee"] != assignee or row["request"] != request:
                    raise StoreError("idempotency_conflict", "Key was used for a different task")
                return self._task(row)
            task_id = str(uuid.uuid4())
            self.connection.execute(
                "INSERT INTO tasks VALUES (?, ?, ?, ?, NULL, 'queued', ?, ?, ?)",
                (task_id, creator, assignee, request, idempotency_key, now, now))
            self._audit(correlation_id, creator, "task.created", task_id, now)
            row = self.connection.execute(
                "SELECT * FROM tasks WHERE task_id=?", (task_id,)).fetchone()
            return self._task(row)

    def task_inbox(self, agent: str) -> list[dict[str, Any]]:
        rows = self.connection.execute(
            "SELECT * FROM tasks WHERE assignee=? AND state='queued' "
            "ORDER BY created_at, task_id LIMIT 50", (agent,)).fetchall()
        return [self._task(row) for row in rows]

    def get_task(self, agent: str, task_id: str) -> dict[str, Any]:
        row = self.connection.execute(
            "SELECT * FROM tasks WHERE task_id=? AND (creator=? OR assignee=?)",
            (task_id, agent, agent)).fetchone()
        if not row:
            raise StoreError("task_not_found", "Task is not visible to this agent")
        return self._task(row)

    def claim_task(self, agent: str, task_id: str,
                   correlation_id: str) -> dict[str, Any]:
        now = _utc_now()
        with self.connection:
            row = self.connection.execute(
                "SELECT * FROM tasks WHERE task_id=?", (task_id,)).fetchone()
            if not row or row["assignee"] != agent:
                raise StoreError("task_not_found", "Task is not assigned to this agent")
            if row["state"] not in {"queued", "running"}:
                raise StoreError("invalid_state", "Task cannot be claimed")
            if row["state"] == "queued":
                self.connection.execute(
                    "UPDATE tasks SET state='running', updated_at=? WHERE task_id=?",
                    (now, task_id))
                self._audit(correlation_id, agent, "task.claimed", task_id, now)
            row = self.connection.execute(
                "SELECT * FROM tasks WHERE task_id=?", (task_id,)).fetchone()
            return self._task(row)

    def complete_task(self, agent: str, task_id: str, result: str,
                      correlation_id: str) -> dict[str, Any]:
        _require_text(result, "result", 20000)
        now = _utc_now()
        with self.connection:
            row = self.connection.execute(
                "SELECT * FROM tasks WHERE task_id=?", (task_id,)).fetchone()
            if not row or row["assignee"] != agent:
                raise StoreError("task_not_found", "Task is not assigned to this agent")
            if row["state"] == "completed" and row["result"] == result:
                return self._task(row)
            if row["state"] != "running":
                raise StoreError("invalid_state", "Only a running task can be completed")
            self.connection.execute(
                "UPDATE tasks SET state='completed', result=?, updated_at=? WHERE task_id=?",
                (result, now, task_id))
            self._audit(correlation_id, agent, "task.completed", task_id, now)
            row = self.connection.execute(
                "SELECT * FROM tasks WHERE task_id=?", (task_id,)).fetchone()
            return self._task(row)

    def cancel_task(self, agent: str, task_id: str,
                    correlation_id: str) -> dict[str, Any]:
        now = _utc_now()
        with self.connection:
            row = self.connection.execute(
                "SELECT * FROM tasks WHERE task_id=?", (task_id,)).fetchone()
            if not row or agent not in (row["creator"], row["assignee"]):
                raise StoreError("task_not_found", "Task is not visible to this agent")
            if row["state"] == "cancelled":
                return self._task(row)
            if row["state"] not in {"queued", "running"}:
                raise StoreError("invalid_state", "Task cannot be cancelled")
            self.connection.execute(
                "UPDATE tasks SET state='cancelled', updated_at=? WHERE task_id=?",
                (now, task_id))
            self._audit(correlation_id, agent, "task.cancelled", task_id, now)
            row = self.connection.execute(
                "SELECT * FROM tasks WHERE task_id=?", (task_id,)).fetchone()
            return self._task(row)

    def send_message(self, sender: str, recipient: str, body: str,
                     idempotency_key: str, correlation_id: str,
                     task_id: str | None = None) -> dict[str, Any]:
        if recipient not in self.agents:
            raise StoreError("invalid_recipient", "Unknown agent")
        _require_text(body, "body", 10000)
        _require_text(idempotency_key, "idempotency_key", 128)
        now = _utc_now()
        with self.connection:
            row = self.connection.execute(
                "SELECT * FROM messages WHERE sender=? AND idempotency_key=?",
                (sender, idempotency_key)).fetchone()
            if row:
                if (row["recipient"], row["body"], row["task_id"]) != (
                        recipient, body, task_id):
                    raise StoreError("idempotency_conflict", "Key was used for a different message")
                return self._message(row)
            if task_id:
                task = self.connection.execute(
                    "SELECT creator, assignee FROM tasks WHERE task_id=?", (task_id,)).fetchone()
                if not task or sender not in (task["creator"], task["assignee"]) or recipient not in (
                        task["creator"], task["assignee"]):
                    raise StoreError("task_not_found", "Message task is not shared by both agents")
            message_id = str(uuid.uuid4())
            self.connection.execute(
                "INSERT INTO messages VALUES (?, ?, ?, ?, ?, 'unread', ?, ?, NULL)",
                (message_id, task_id, sender, recipient, body, idempotency_key, now))
            self._audit(correlation_id, sender, "message.sent", message_id, now)
            row = self.connection.execute(
                "SELECT * FROM messages WHERE message_id=?", (message_id,)).fetchone()
            return self._message(row)

    def message_inbox(self, agent: str) -> list[dict[str, Any]]:
        rows = self.connection.execute(
            "SELECT * FROM messages WHERE recipient=? AND state='unread' "
            "ORDER BY created_at, message_id LIMIT 50", (agent,)).fetchall()
        return [self._message(row) for row in rows]

    def acknowledge_message(self, agent: str, message_id: str,
                            correlation_id: str) -> dict[str, Any]:
        now = _utc_now()
        with self.connection:
            row = self.connection.execute(
                "SELECT * FROM messages WHERE message_id=?", (message_id,)).fetchone()
            if not row or row["recipient"] != agent:
                raise StoreError("message_not_found", "Message is not addressed to this agent")
            if row["state"] == "unread":
                self.connection.execute(
                    "UPDATE messages SET state='acknowledged', acknowledged_at=? "
                    "WHERE message_id=?", (now, message_id))
                self._audit(correlation_id, agent, "message.acknowledged", message_id, now)
            row = self.connection.execute(
                "SELECT * FROM messages WHERE message_id=?", (message_id,)).fetchone()
            return self._message(row)
