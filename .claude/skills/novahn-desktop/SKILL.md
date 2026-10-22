---
name: novahn-desktop
description: Receive Unity work in desktop AI chat through Novahn Studio. Connect over the existing Unity MCP, act as the primary coordinator, record goals, delegate to active workers and report results without submitting requests in the Unity Editor.
---

# Novahn desktop workflow

Use for Unity work in a project with Novahn Studio. Respond in the user's language; the Editor's language does not control chat language.

## Connect from chat

1. Read `mcpforunity://instances` and `mcpforunity://editor/state` using the existing Unity MCP connection. Pin the intended instance when several Editors are connected. If the connection is unavailable, report the actual connection error; Unity must be open and MCP connected.
2. Read `mcpforunity://custom-tools`. Find `novahn_workspace`. Call it directly if exposed, otherwise use `execute_custom_tool(tool_name="novahn_workspace", parameters={...})`.
3. Read `status.availableClients`, then call `{"action":"connect","arguments":{"client":"<your-own-client-id>"}}`. The AI chatting with the user is the primary AI: connecting with your own app's ID makes you the primary, whatever was selected before; never default to Codex or use another app's ID. If you cannot tell your client ID, ask the user. Team members are command-line AIs added with `add_agent`. Connecting names the primary automatically and starts its Hub without rewriting client configuration. Use the returned identity. If the Hub is starting, retry the dependent action after the provided delay using the same idempotency key.
4. For an authorized substantive user goal, call `request` with `arguments: {title, request, idempotency_key}`. Keep the key stable across retries. Claim the returned `taskId` with `claim` and `arguments: {task_id}`. Do not require the user to enter the same request in Unity. Do not record read-only questions or duplicate the same goal on each conversational follow-up.

## Execute and coordinate

- The gateway is for the trusted primary AI. Use ordinary Unity MCP tools to edit the correct project. Follow its AGENTS.md and report actual results.
- `status` shows members and observed activity; configuration alone does not mean an AI is running. Work directly if no worker is active. Delegate only within the user's authorized scope and when the worker session is available.
- `delegate` takes `{title, request, assignee, idempotency_key, parent_id?}`. Requests must state scope, relevant paths, acceptance criteria and dependencies. Avoid overlapping writes to scenes, prefabs or files. Use `get` with `{task_id}` to read real results and `events` with `{after}` to observe progress.
- Read worker questions with `messages`, reply inside the authorized team scope with `message` and `{recipient, body, idempotency_key, task_id?}`, then acknowledge handled messages with `ack` and `{message_id}`. These actions do not post to desktop chats or external messaging services.
- `request` and `delegate` accept optional `scheduled_at`, a future ISO8601 timestamp with an explicit timezone. Scheduled delivery requires Unity and Hub running; it does not launch or wake a model. Return the saved schedule honestly.
- Complete the primary goal with `complete` and `{task_id, result}` only when required work is finished. Use `cancel` for a cancelled task. Ask the user for missing requirements only when necessary.

## Add workers from desktop chat

When the user asks to add another AI, call `add_agent` with `{client, responsibility}`. The display name is automatic; never require the user to type it. Client IDs include `codex`, `claudecode`, `claudedesktop`, `cursor` and the IDs supported by the current client registry. Adding a member may restart the project Hub and interrupt outstanding wait calls; task data persists.

Use `setup_client` with `{agent_id}` to merge that client's MCP configuration, back it up and install its role skills. This is a configuration write; use it when onboarding is requested. It returns the join prompt for the worker session. A client may require reconnecting MCP or starting a new session to discover its endpoint and skills. `install_skills` installs skills without changing MCP configuration.

Workers use only their assigned authenticated Hub endpoint for `hub.task.inbox/wait/claim/complete` and `hub.message.*`. They must not use the primary gateway to impersonate the coordinator. The Hub stores and routes work; it cannot wake an idle desktop chat, open conversations or launch AI models. Do not send messages to external chats without the user's authorization.

Follow the project's instructions about package copies and source synchronization. If a project is using a preview pending review, obtain the required user approval before applying it to its source repository.
