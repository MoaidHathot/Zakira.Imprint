---
layout: default
title: MCP Server Configuration
parent: Guides
nav_order: 3
description: "Distribute MCP server configurations with your skill packages."
permalink: /guides/mcp-server-configuration
---

# MCP Server Configuration
{: .fs-9 }

Distribute Model Context Protocol (MCP) server configurations with your skill packages.
{: .fs-6 .fw-300 }

---

## Overview

MCP (Model Context Protocol) servers extend AI assistants with additional capabilities like database queries, API access, or tool execution. Imprint allows you to distribute MCP server configurations alongside your skills.

---

## Prerequisites

- A skill package (either skills-only or code+skills)
- An MCP server to distribute (npm package, binary, or script)

---

## Step 1: Create the MCP Directory

In your skill package project:

```bash
mkdir mcp
```

---

## Step 2: Create the Fragment File

Create `mcp/YourPackageName.mcp.json`:

```json
{
  "servers": {
    "your-server-name": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@your-org/your-mcp-server"]
    }
  }
}
```

{: .important }
> Use your package name as the filename prefix (e.g., `MyOrg.Skills.mcp.json`) to avoid conflicts.

---

## Step 3: Update Your Project File

Add the MCP fragment to your `<Imprint>` items:

```xml
<ItemGroup>
  <!-- Existing skill files -->
  <Imprint Include="skills\**\*" />
  
  <!-- Add MCP fragments -->
  <Imprint Include="mcp\*.mcp.json" Type="Mcp" />
</ItemGroup>
```

{: .warning }
> The `Type="Mcp"` attribute is required. Without it, the file would be treated as a skill file.

---

## MCP Fragment Format

### Basic Structure

```json
{
  "servers": {
    "server-name": {
      "type": "stdio",
      "command": "command-to-run",
      "args": ["arg1", "arg2"]
    }
  }
}
```

### With Environment Variables

```json
{
  "servers": {
    "database-server": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@myorg/db-mcp-server"],
      "env": {
        "DB_HOST": "${env:DATABASE_HOST}",
        "DB_PORT": "${env:DATABASE_PORT}"
      }
    }
  }
}
```

### With Working Directory

```json
{
  "servers": {
    "local-server": {
      "type": "stdio",
      "command": "node",
      "args": ["./server.js"],
      "cwd": "${workspaceFolder}/tools"
    }
  }
}
```

### Multiple Servers

```json
{
  "servers": {
    "database": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@myorg/db-server"]
    },
    "api-client": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@myorg/api-server"]
    },
    "file-manager": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@myorg/file-server"]
    }
  }
}
```

---

## Server Configuration Properties

| Property | Required | Description |
|:---------|:---------|:------------|
| `type` | No | Connection type (usually `"stdio"`) |
| `command` | Yes | The executable to run |
| `args` | No | Array of command-line arguments |
| `env` | No | Environment variables |
| `cwd` | No | Working directory |

---

## Common Patterns

### npm Package Server

Most MCP servers are distributed as npm packages:

```json
{
  "servers": {
    "my-server": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@myorg/my-mcp-server@latest"]
    }
  }
}
```

The `-y` flag auto-confirms npx prompts.

### Python Server

For Python-based servers:

```json
{
  "servers": {
    "python-server": {
      "type": "stdio",
      "command": "python",
      "args": ["-m", "my_mcp_server"]
    }
  }
}
```

Or with uvx:

```json
{
  "servers": {
    "python-server": {
      "type": "stdio",
      "command": "uvx",
      "args": ["my-mcp-server"]
    }
  }
}
```

### Docker-Based Server

```json
{
  "servers": {
    "docker-server": {
      "type": "stdio",
      "command": "docker",
      "args": ["run", "-i", "--rm", "myorg/mcp-server:latest"]
    }
  }
}
```

### Local Binary

```json
{
  "servers": {
    "local-binary": {
      "type": "stdio",
      "command": "${workspaceFolder}/tools/mcp-server.exe",
      "args": ["--config", "${workspaceFolder}/config.json"]
    }
  }
}
```

---

## Environment Variables

### Reference User Environment

```json
{
  "env": {
    "API_KEY": "${env:MY_API_KEY}"
  }
}
```

The `${env:VAR_NAME}` syntax reads from the user's environment.

### Static Values

```json
{
  "env": {
    "LOG_LEVEL": "debug",
    "VERSION": "1.0.0"
  }
}
```

### Workspace Variables

```json
{
  "env": {
    "PROJECT_ROOT": "${workspaceFolder}"
  }
}
```

---

## Multiple Packages with MCP

When multiple skill packages provide MCP fragments, all servers are merged:

**Package A:**
```json
{
  "servers": {
    "database": { ... }
  }
}
```

**Package B:**
```json
{
  "servers": {
    "api-client": { ... }
  }
}
```

**Result in mcp.json:**
```json
{
  "servers": {
    "database": { ... },
    "api-client": { ... }
  }
}
```

### Server Name Conflicts

If two packages define the same server name, the later one wins. To avoid conflicts:

- Prefix server names with your package/org name
- Use unique, descriptive names

```json
{
  "servers": {
    "myorg-database": { ... },
    "myorg-api": { ... }
  }
}
```

---

## Agent-Specific Behavior

Imprint adapts the MCP format for each agent:

### VS Code / Copilot

**Location:** `.vscode/mcp.json`
**Root key:** `servers`

```json
{
  "servers": {
    "my-server": { ... }
  }
}
```

### Claude

**Location:** `.claude/mcp.json`
**Root key:** `mcpServers`

```json
{
  "mcpServers": {
    "my-server": { ... }
  }
}
```

### Cursor

**Location:** `.cursor/mcp.json`
**Root key:** `mcpServers`

```json
{
  "mcpServers": {
    "my-server": { ... }
  }
}
```

### Roo Code

**Location:** `.roo/mcp.json`
**Root key:** `mcpServers`

```json
{
  "mcpServers": {
    "my-server": { ... }
  }
}
```

You write one fragment with `servers`, and Imprint handles the translation.

---

## Testing Your MCP Configuration

### Step 1: Build Your Package

```bash
dotnet build
```

### Step 2: Check the Generated mcp.json

```bash
cat .vscode/mcp.json   # Copilot
cat .claude/mcp.json   # Claude
cat .cursor/mcp.json   # Cursor
cat .roo/mcp.json     # Roo Code
```

### Step 3: Verify Server Starts

Test that your server command works:

```bash
npx -y @your-org/your-mcp-server
```

### Step 4: Test in AI Assistant

Open your AI assistant and verify it can connect to the server.

---

## Troubleshooting

### Server Not Appearing

1. Check that `Type="Mcp"` is set on the item
2. Verify the fragment file is valid JSON
3. Check the build output for errors

### Server Won't Start

1. Test the command manually in terminal
2. Check environment variables are available
3. Verify the server package is accessible

### Merging Issues

1. Check for server name conflicts
2. Verify JSON syntax in all fragments
3. Check `.imprint/manifest.json` for managed servers

---

## Complete Example

### Project Structure

```
MyOrg.Skills.Database/
├── MyOrg.Skills.Database.csproj
├── skills/
│   └── database/
│       └── SKILL.md
└── mcp/
    └── MyOrg.Skills.Database.mcp.json
```

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Zakira.Imprint.Sdk" Version="1.0.0-preview">
      <PrivateAssets>compile</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Imprint Include="skills\**\*" />
    <Imprint Include="mcp\*.mcp.json" Type="Mcp" />
  </ItemGroup>
</Project>
```

### MCP Fragment

```json
{
  "servers": {
    "myorg-database": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@myorg/database-mcp-server@^1.0.0"],
      "env": {
        "DB_CONNECTION": "${env:MYORG_DATABASE_CONNECTION}",
        "LOG_LEVEL": "info"
      }
    }
  }
}
```

### Skill File

```markdown
# Database Skills

This project uses the MyOrg Database MCP server for database operations.

## Available Tools

### query

Execute read-only SQL queries:

```
Use the database query tool to: SELECT * FROM users WHERE active = true
```

### execute

Execute write operations (INSERT, UPDATE, DELETE):

```
Use the database execute tool to insert a new user with email "user@example.com"
```

## Setup

Ensure the `MYORG_DATABASE_CONNECTION` environment variable is set:

```bash
export MYORG_DATABASE_CONNECTION="Server=localhost;Database=mydb;..."
```

## Best Practices

- Always use parameterized queries through the MCP server
- Use `query` for read operations, `execute` for writes
- The server handles connection pooling automatically
```

---

## Next Steps

- [Consuming Packages]({{ site.baseurl }}/guides/consuming-packages) - How consumers use your package
- [Publishing Packages]({{ site.baseurl }}/guides/publishing-packages) - Publish to NuGet.org
- [Configuration Reference]({{ site.baseurl }}/reference/configuration) - All MCP options
