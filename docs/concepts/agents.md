---
layout: default
title: Multi-Agent Support
parent: Concepts
nav_order: 2
description: "How Imprint targets multiple AI assistants simultaneously."
permalink: /concepts/agents
---

# Multi-Agent Support
{: .fs-9 }

Target GitHub Copilot, Claude, Cursor, Roo Code, and more - all at once.
{: .fs-6 .fw-300 }

---

## Supported Agents

Imprint has built-in support for four AI assistants:

| Agent | Skills Directory | MCP Config File | MCP Root Key |
|:------|:-----------------|:----------------|:-------------|
| `copilot` | `.github/skills/` | `.vscode/mcp.json` | `servers` |
| `claude` | `.claude/skills/` | `.claude/mcp.json` | `mcpServers` |
| `cursor` | `.cursor/rules/` | `.cursor/mcp.json` | `mcpServers` |
| `roo` | `.roo/rules/` | `.roo/mcp.json` | `mcpServers` |

### Agent-Specific Conventions

Each AI assistant has its own conventions:

**GitHub Copilot**
- Skills in `.github/skills/` (can be nested in subdirectories)
- MCP config in `.vscode/mcp.json` (VS Code integration)
- Uses `servers` as the root key in `mcp.json`

**Claude**
- Skills in `.claude/skills/`
- MCP config in `.claude/mcp.json`
- Uses `mcpServers` as the root key in `mcp.json`

**Cursor**
- Rules in `.cursor/rules/` (note: "rules" not "skills")
- MCP config in `.cursor/mcp.json`
- Uses `mcpServers` as the root key in `mcp.json`

**Roo Code**
- Rules in `.roo/rules/` (note: "rules" not "skills")
- MCP config in `.roo/mcp.json`
- Uses `mcpServers` as the root key in `mcp.json`

---

## Agent Detection

By default, Imprint auto-detects which agents to target by scanning for their directories:

```
Project Directory
├── .github/         ← Copilot detected
├── .claude/         ← Claude detected
├── .cursor/         ← Cursor detected
├── .roo/            ← Roo Code detected
└── MyProject.csproj
```

The detection logic checks for the existence of these directories:

| Directory | Agent |
|:----------|:------|
| `.github/` | `copilot` |
| `.claude/` | `claude` |
| `.cursor/` | `cursor` |
| `.roo/` | `roo` |

---

## Resolution Priority

Agent resolution follows a four-tier priority system:

### Tier 1: Explicit Configuration (Highest Priority)

If `ImprintTargetAgents` is set, it's used directly:

```xml
<PropertyGroup>
  <ImprintTargetAgents>copilot;claude</ImprintTargetAgents>
</PropertyGroup>
```

Result: Skills deployed to Copilot and Claude only.

### Tier 2: Auto-Detection (Default)

If `ImprintAutoDetectAgents` is `true` (the default), Imprint scans for agent directories:

```xml
<PropertyGroup>
  <ImprintAutoDetectAgents>true</ImprintAutoDetectAgents>
</PropertyGroup>
```

Result: Skills deployed to all detected agents.

### Tier 3: Default Agents

If auto-detection finds nothing, `ImprintDefaultAgents` is used:

```xml
<PropertyGroup>
  <ImprintDefaultAgents>copilot</ImprintDefaultAgents>
</PropertyGroup>
```

Result: Skills deployed to the default agent(s).

### Tier 4: Ultimate Fallback

If everything else is empty, Imprint defaults to `copilot`.

---

## Configuration Examples

### Target Specific Agents Only

```xml
<PropertyGroup>
  <!-- Only target Claude, ignore everything else -->
  <ImprintTargetAgents>claude</ImprintTargetAgents>
</PropertyGroup>
```

### Disable for CI/CD

```xml
<PropertyGroup Condition="'$(CI)' == 'true'">
  <!-- Don't install skills in CI environments -->
  <ImprintTargetAgents></ImprintTargetAgents>
  <ImprintAutoDetectAgents>false</ImprintAutoDetectAgents>
</PropertyGroup>
```

### Change Default Agent

```xml
<PropertyGroup>
  <!-- If no agents detected, use Claude instead of Copilot -->
  <ImprintDefaultAgents>claude</ImprintDefaultAgents>
</PropertyGroup>
```

### Target Multiple Specific Agents

```xml
<PropertyGroup>
  <!-- Explicitly target Copilot and Cursor -->
  <ImprintTargetAgents>copilot;cursor</ImprintTargetAgents>
</PropertyGroup>
```

---

## Unknown Agents

You can target agents that aren't in the built-in list. Imprint uses a fallback convention:

```xml
<PropertyGroup>
  <ImprintTargetAgents>windsurf</ImprintTargetAgents>
</PropertyGroup>
```

For unknown agents, Imprint assumes:
- Skills directory: `.windsurf/skills/`
- MCP config: `.windsurf/mcp.json`
- MCP root key: `servers`

This allows targeting new AI assistants without waiting for an SDK update.

---

## How Skills Are Copied

When Imprint copies skills to agent directories, it preserves the directory structure:

**Package Structure:**
```
skills/
├── authentication/
│   └── SKILL.md
├── security/
│   ├── SKILL.md
│   └── examples.md
└── README.md
```

**Installed to Copilot (.github/skills/):**
```
.github/skills/
├── authentication/
│   └── SKILL.md
├── security/
│   ├── SKILL.md
│   └── examples.md
└── README.md
```

**Installed to Cursor (.cursor/rules/):**
```
.cursor/rules/
├── authentication/
│   └── SKILL.md
├── security/
│   ├── SKILL.md
│   └── examples.md
└── README.md
```

**Installed to Roo Code (.roo/rules/):**
```
.roo/rules/
├── authentication/
│   └── SKILL.md
├── security/
│   ├── SKILL.md
│   └── examples.md
└── README.md
```

The same files are copied to each agent's native directory.

---

## MCP Config Differences

Each agent may have slightly different MCP configuration formats:

**Copilot/VS Code (`.vscode/mcp.json`):**
```json
{
  "servers": {
    "my-server": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@my-org/my-server"]
    }
  }
}
```

**Claude (`.claude/mcp.json`):**
```json
{
  "mcpServers": {
    "my-server": {
      "command": "npx",
      "args": ["-y", "@my-org/my-server"]
    }
  }
}
```

Imprint handles these differences automatically when merging MCP fragments.

---

## Creating Agent Directories

If you want Imprint to auto-detect an agent, create its directory:

```bash
# Enable Copilot detection
mkdir .github

# Enable Claude detection
mkdir .claude

# Enable Cursor detection
mkdir .cursor

# Enable Roo Code detection
mkdir .roo
```

Then run `dotnet build` - Imprint will detect and target those agents.

---

## Checking Which Agents Are Targeted

After a build, check the manifest to see which agents were targeted:

```bash
cat .imprint/manifest.json
```

Output:
```json
{
  "version": 2,
  "packages": {
    "MyOrg.Skills": {
      "files": {
        "copilot": [".github/skills/SKILL.md"],
        "claude": [".claude/skills/SKILL.md"]
      }
    }
  }
}
```

---

## Next Steps

- [MCP Integration]({{ site.baseurl }}/concepts/mcp-integration) - How MCP servers are distributed
- [Configuration Reference]({{ site.baseurl }}/reference/configuration) - All agent-related properties
