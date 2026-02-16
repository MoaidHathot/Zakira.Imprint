---
layout: default
title: Reference
nav_order: 6
has_children: true
permalink: /reference/
---

# Reference Documentation

Complete technical reference for Zakira.Imprint SDK.

## Contents

### [Configuration]({% link reference/configuration.md %})
All MSBuild properties and their default values for configuring Imprint behavior.

### [Architecture]({% link reference/architecture.md %})
Deep-dive into SDK internals, MSBuild tasks, and how the system works under the hood.

### [API Reference]({% link reference/api.md %})
Detailed documentation for MSBuild tasks and item groups.

## Quick Reference

### MSBuild Properties

| Property | Default | Description |
|----------|---------|-------------|
| `ImprintAutoDetectAgents` | `true` | Auto-detect AI agents in project |
| `ImprintTargetAgents` | *(empty)* | Explicit agents (disables auto-detect) |
| `ImprintDefaultAgents` | `copilot` | Fallback when no agents detected |
| `ImprintPrefixSkills` | `false` | Add package prefix to skill folders |
| `ImprintEnabledByDefault` | `true` | Enable skills by default |

### Item Groups

| Item | Purpose |
|------|---------|
| `<Imprint>` | Declare skill/MCP files in package |
| `<ImprintContent>` | Auto-generated from package .targets |
| `<ImprintMcpFragment>` | MCP server configuration fragments |

### Agent Paths

| Agent | Skills Path | MCP Config |
|-------|-------------|------------|
| Copilot | `.github/skills/` | `.vscode/mcp.json` |
| Claude | `.claude/skills/` | `.claude/mcp.json` |
| Cursor | `.cursor/rules/` | `.cursor/mcp.json` |
| Roo Code | `.roo/rules/` | `.roo/mcp.json` |
