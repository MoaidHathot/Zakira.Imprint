---
layout: default
title: MCP Integration
parent: Concepts
nav_order: 3
description: "How Imprint distributes Model Context Protocol (MCP) server configurations."
permalink: /concepts/mcp-integration
---

# MCP Integration
{: .fs-9 }

Distribute Model Context Protocol (MCP) server configurations alongside your skills.
{: .fs-6 .fw-300 }

---

## What is MCP?

The [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) is a standard for connecting AI assistants to external tools and data sources. MCP servers provide capabilities like:

- Database queries
- API integrations
- File system access
- Code execution
- External service connections

AI assistants can connect to MCP servers to extend their capabilities beyond their base training.

---

## MCP in Imprint

Imprint allows skill packages to include MCP server configurations that are automatically merged into each AI assistant's `mcp.json` file.

### The Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  Skill Package                                                  │
│  └── mcp/MyPackage.mcp.json                                     │
│      {                                                          │
│        "servers": {                                             │
│          "my-database": {                                       │
│            "type": "stdio",                                     │
│            "command": "npx",                                    │
│            "args": ["-y", "@myorg/db-mcp-server"]              │
│          }                                                      │
│        }                                                        │
│      }                                                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼ dotnet build
┌─────────────────────────────────────────────────────────────────┐
│  Consumer Project                                               │
│  ├── .vscode/mcp.json  (Copilot)                               │
│  │   {                                                          │
│  │     "servers": {                                             │
│  │       "my-database": { ... }    ← merged                     │
│  │     }                                                        │
│  │   }                                                          │
│  └── .claude/mcp.json  (Claude)                                │
│      {                                                          │
│        "mcpServers": {                                          │
│          "my-database": { ... }    ← merged                     │
│        }                                                        │
│      }                                                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## Creating MCP Fragments

### Step 1: Create the MCP Directory

In your skill package project:

```bash
mkdir mcp
```

### Step 2: Create the Fragment File

Create `mcp/YourPackageName.mcp.json`:

```json
{
  "servers": {
    "server-name": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@your-org/your-mcp-server"]
    }
  }
}
```

### Step 3: Include in Your Project

```xml
<ItemGroup>
  <Imprint Include="mcp\*.mcp.json" Type="Mcp" />
</ItemGroup>
```

{: .important }
> The `Type="Mcp"` attribute tells Imprint this is an MCP fragment, not a skill file.

---

## Fragment Format

MCP fragments use a simplified format with just the `servers` object:

```json
{
  "servers": {
    "server-one": {
      "type": "stdio",
      "command": "node",
      "args": ["path/to/server.js"],
      "env": {
        "API_KEY": "${env:MY_API_KEY}"
      }
    },
    "server-two": {
      "type": "stdio",
      "command": "python",
      "args": ["-m", "my_mcp_server"]
    }
  }
}
```

### Server Configuration Options

| Property | Required | Description |
|:---------|:---------|:------------|
| `type` | No | Server type (typically `"stdio"`) |
| `command` | Yes | The command to run |
| `args` | No | Array of command arguments |
| `env` | No | Environment variables |
| `cwd` | No | Working directory |

---

## Merge Behavior

### Multiple Packages

When multiple skill packages provide MCP fragments, all servers are merged:

**Package A's fragment:**
```json
{
  "servers": {
    "database": { ... }
  }
}
```

**Package B's fragment:**
```json
{
  "servers": {
    "api-server": { ... }
  }
}
```

**Result in mcp.json:**
```json
{
  "servers": {
    "database": { ... },
    "api-server": { ... }
  }
}
```

### User Servers Are Preserved

If the user has manually added servers to `mcp.json`, Imprint preserves them:

**Before build (user-defined):**
```json
{
  "servers": {
    "my-custom-server": { ... }
  }
}
```

**After build (merged):**
```json
{
  "servers": {
    "my-custom-server": { ... },
    "package-server": { ... }
  }
}
```

### Other Properties Are Preserved

Properties like `inputs` are preserved through all operations:

```json
{
  "inputs": {
    "api-key": {
      "type": "promptString",
      "description": "Enter your API key"
    }
  },
  "servers": {
    "managed-server": { ... },
    "user-server": { ... }
  }
}
```

---

## Managed vs User Servers

Imprint tracks which servers it manages in the manifest:

```json
{
  "mcp": {
    "copilot": {
      "path": ".vscode/mcp.json",
      "managedServers": ["database", "api-server"]
    }
  }
}
```

On clean (`dotnet clean`), only managed servers are removed. User-defined servers are never touched.

---

## Agent-Specific Differences

Different AI assistants use slightly different MCP formats:

### VS Code / Copilot

**File:** `.vscode/mcp.json`
**Root Key:** `servers`

```json
{
  "servers": {
    "my-server": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@org/server"]
    }
  }
}
```

### Claude

**File:** `.claude/mcp.json`
**Root Key:** `mcpServers`

```json
{
  "mcpServers": {
    "my-server": {
      "command": "npx",
      "args": ["-y", "@org/server"]
    }
  }
}
```

### Cursor

**File:** `.cursor/mcp.json`
**Root Key:** `mcpServers`

```json
{
  "mcpServers": {
    "my-server": {
      "command": "npx",
      "args": ["-y", "@org/server"]
    }
  }
}
```

Imprint handles these differences automatically. You write one fragment with `servers`, and Imprint translates to the correct format for each agent.

---

## Idempotent Writes

Imprint compares the new `mcp.json` content with the existing file before writing. If they're identical, no write occurs. This prevents:

- Unnecessary file system changes
- Git noise from timestamp-only changes
- File watcher triggers

---

## Clean Behavior

When you run `dotnet clean`:

1. Imprint reads the manifest to find managed servers
2. For each agent's `mcp.json`:
   - Removes all managed servers
   - Preserves user-defined servers
   - Preserves other properties (`inputs`, etc.)
   - Deletes the file if it becomes empty

---

## Example: Complete MCP Package

Here's a complete example of a skill package with MCP integration:

**Project Structure:**
```
MyOrg.Skills.Database/
├── MyOrg.Skills.Database.csproj
├── skills/
│   └── database/
│       └── SKILL.md
└── mcp/
    └── MyOrg.Skills.Database.mcp.json
```

**MyOrg.Skills.Database.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Zakira.Imprint.Sdk" Version="1.1.0">
      <PrivateAssets>compile</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Imprint Include="skills\**\*" />
    <Imprint Include="mcp\*.mcp.json" Type="Mcp" />
  </ItemGroup>
</Project>
```

**skills/database/SKILL.md:**
```markdown
# Database Skills

When working with the database MCP server:

1. Use the `query` tool to execute read-only SQL queries
2. Use the `execute` tool for write operations
3. Always use parameterized queries to prevent SQL injection
```

**mcp/MyOrg.Skills.Database.mcp.json:**
```json
{
  "servers": {
    "myorg-database": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@myorg/database-mcp-server"],
      "env": {
        "DB_CONNECTION": "${env:MYORG_DB_CONNECTION}"
      }
    }
  }
}
```

---

## Next Steps

- [MCP Server Configuration Guide](/guides/mcp-server-configuration) - Step-by-step MCP setup
- [Configuration Reference](/reference/configuration) - MCP-related properties
