---
name: mcp-source
description: Switch MCP for Unity package source in connected Unity projects. Use /mcp-source [main|beta|branch|local] to swap between upstream releases, your remote branch, or local dev checkout.
---

# Switch MCP for Unity Package Source

You are switching the `com.novahn.unity-mcp` package source in one or more Unity projects.

## allowed-tools

Bash, Read, Edit, ReadMcpResourceTool, ListMcpResourcesTool

## Instructions

### 1. Parse arguments

The user's argument is: `$ARGUMENTS`

Valid values: `main`, `beta`, `branch`, `local`, or empty.

If empty or not one of the four valid values, ask the user to choose:
- **main** — upstream main branch (stable releases)
- **beta** — upstream beta branch (pre-release)
- **branch** — your current remote branch (for testing a PR)
- **local** — local file reference to your checkout (for live dev iteration)

### 2. Detect repo context

Run these git commands from the current working directory to find the unity-mcp repo:

```bash
git rev-parse --show-toplevel    # → repo_root
git rev-parse --abbrev-ref HEAD  # → branch_name
git remote get-url origin        # → origin_url
```

Convert SSH origins to HTTPS: transform `git@host:group/repo.git` to `https://host/group/repo.git`.

### 3. Discover target Unity projects

Try two approaches to find `Packages/manifest.json` files to update:

**Approach A — MCP resources (preferred):**
Read `mcpforunity://project/info` for each connected Unity instance (use `ListMcpResourcesTool` to find available instances). Extract `projectRoot` and use `{projectRoot}/Packages/manifest.json`.

**Approach B — filesystem fallback:**
If no MCP instances are connected, search upward from the current working directory for `Packages/manifest.json` files using Bash:
```bash
find "$(pwd)" -maxdepth 3 -name "manifest.json" -path "*/Packages/manifest.json" 2>/dev/null
```

If multiple manifests are found, update all of them (confirming with the user first).

### 4. Build the package URL

Based on the user's selection, construct the dependency value:

| Selection | URL | Package ID |
|-----------|-----|------------|
| `main`    | `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main` | `com.coplaydev.unity-mcp` |
| `beta`    | `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#beta` | `com.coplaydev.unity-mcp` |
| `branch`  | `{origin_https}?path=/MCPForUnity#{branch_name}` | Read `MCPForUnity/package.json` `name` from this checkout |
| `local`   | `file:{repo_root}/MCPForUnity` | Read `MCPForUnity/package.json` `name` from this checkout |

For `branch`: use the HTTPS-normalized origin URL and current git branch name.
For `local`: use the absolute path to the repo root with `file:` prefix (no `//`), e.g. `file:/Users/davidsarno/unity-mcp/MCPForUnity`.
For `branch` and `local`, read the package ID from `MCPForUnity/package.json`; this Novahn fork uses `com.novahn.unity-mcp`.

### 5. Update each manifest

For each discovered `manifest.json`:

1. Read the file with the Read tool
2. Find either the Novahn or CoplayDev Unity package dependency
3. Remove the old package-ID entry if it differs from the selected package ID, then set the selected package ID to the new URL
4. Report what was changed: old value → new value, file path

### 6. Report results

After updating, tell the user:
- Which manifests were updated
- The old and new package source values
- Remind them: "Unity will re-resolve the package automatically. If it doesn't, open Package Manager and click Refresh."
