---
id: install
slug: /getting-started/install
title: Install
sidebar_label: Install
description: Add Novahn MCP for Unity to your Unity project and connect AI clients.
---

# Install

Install the Novahn fork with its Git URL. The Unity Package Manager fetches the Unity package from the `main` branch.

## Prerequisites

- **Unity 2021.3 LTS or newer** — [Download Unity](https://unity.com/download)
- **Python 3.10+** with [`uv`](https://docs.astral.sh/uv/getting-started/installation/) — the setup wizard guides you through both if missing
- **An MCP client** — [Claude Desktop](https://claude.ai/download), [Claude Code](https://docs.anthropic.com/en/docs/claude-code), [Cursor](https://www.cursor.com/), [VS Code Copilot](https://code.visualstudio.com/docs/copilot/overview), [GitHub Copilot CLI](https://docs.github.com/en/copilot/concepts/agents/about-copilot-cli), [Windsurf](https://windsurf.com/), [Cline](https://cline.bot/), [OpenClaw](https://openclaw.ai/), and more

If your project already uses `com.coplaydev.unity-mcp`, remove that package before adding `com.novahn.unity-mcp` so Unity does not load both copies.

## Add from Git URL

This path needs `git` on your PATH (the Package Manager runs it). If it reports `Error when executing git command`, see [troubleshooting](../guides/troubleshooting.md#package-manager-error-when-executing-git-command--not-in-a-git-directory).

In Unity, open **Window → Package Manager**, click the **`+`** button, choose **Add package from git URL...**, and paste:

```text
https://gitlab.com/novahn/unity-mcp.git?path=/MCPForUnity#main
```

## Start the server and connect

After import, Novahn MCP for Unity opens a **setup wizard** automatically.

1. Confirm Python and `uv` are installed — the wizard guides you through both if missing.
2. Click **Done**. Once dependencies are green, a list of MCP clients detected on your machine appears.
3. Pick the clients you want to configure and click **Configure Selected**.

You can return to this UI anytime via **Novahn MCP → Toggle MCP Window** to start/stop the server, switch transport, or reconfigure clients. Use **HTTP Local** when several AI clients should share one Unity Editor. Enable **Auto-start MCP on Unity startup** in the Connect tab if Unity should start the local MCP server automatically.

### First prompt

Try one of these in your MCP client:

> Create a red, blue, and yellow cube in the current scene.

> Build a simple player controller with WASD movement and a double-jump.

> List every script in `Assets/Scripts` and tell me which ones reference `Rigidbody`.

## Per-client notes

- **Claude Desktop** only supports stdio. MCP for Unity will silently configure it that way even if you have HTTP selected elsewhere.
- **Cursor, Antigravity, OpenClaw** still require enabling an MCP toggle or plugin in their own settings after auto-configuration.
- **OpenClaw** also needs the `openclaw-mcp-bridge` plugin enabled and follows the currently selected MCP for Unity transport.
- **Claude Code, VS Code, Windsurf, Cline, and the CLI clients** auto-connect after configuration.

Detailed per-client setup lives in the [MCP Client Configurators guide](/guides/client-configurators).

## Manual MCP client configuration

If auto-configuration doesn't work for your client, add this to your client's MCP config file:

### HTTP (default — Cursor, Windsurf, Antigravity, VS Code, Cline, etc.)

```json
{
  "mcpServers": {
    "unityMCP": {
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

### VS Code

```json
{
  "servers": {
    "unityMCP": {
      "type": "http",
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

### Stdio (Claude Desktop, or any client without HTTP)

**macOS / Linux:**

```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "uvx",
      "args": ["--from", "mcpforunityserver", "mcp-for-unity", "--transport", "stdio"]
    }
  }
}
```

**Windows:**

```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "C:/Users/YOUR_USERNAME/AppData/Local/Microsoft/WinGet/Links/uvx.exe",
      "args": ["--from", "mcpforunityserver", "mcp-for-unity", "--transport", "stdio"]
    }
  }
}
```

## Troubleshooting

- **Unity Bridge not connecting** — Open **Novahn MCP → Toggle MCP Window** and check the status panel. Restart Unity if needed.
- **Server not starting** — Verify `uv --version` works in your terminal. Check the MCP for Unity log for errors.
- **Client not connecting** — Confirm the HTTP server is running on `localhost:8080` and the URL in your client config matches.

For Cursor / VS Code / Windsurf and Claude Code troubleshooting, see the [GitHub Wiki](https://github.com/CoplayDev/unity-mcp/wiki) *(migrating into this site)*.

Still stuck? [Open a Novahn issue](https://gitlab.com/novahn/unity-mcp/-/issues) or [join the upstream Discord](https://discord.gg/y4p8KfzrN4).
