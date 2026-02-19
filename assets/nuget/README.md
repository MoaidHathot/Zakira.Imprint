# Imprint

**Distribute AI Skills and MCP configurations via NuGet packages**

[![Documentation](https://img.shields.io/badge/docs-moaidhathot.github.io%2FZakira.Imprint-blue)](https://moaidhathot.github.io/Zakira.Imprint/)

## Overview

Imprint is a pattern for distributing AI Skills (those `SKILLS.md` files for GitHub Copilot, Claude, Cursor, and other AI assistants) and MCP Server configuration via NuGet packages. When you add an Imprint package to your project:

1. **On `dotnet build`**: Skills are automatically copied to each AI agent's native directory
2. **On `dotnet clean`**: Skills are removed (including empty parent directories)
3. **Multi-agent support**: Targets Copilot, Claude, and Cursor simultaneously — each gets files in its native location (if exists)
4. **All file types supported**: Not just `.md` — scripts, configs, and any other files in the `skills/` folder are included
5. **MCP Server Injection**: Packages can inject [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server configurations into each agent's `mcp.json`
6. **Code + Skills**: Packages can ship both a compiled DLL library **and** AI skills — consumers get runtime APIs and AI guidance from a single NuGet install

This enables scenarios like:
- **Compliance skills**: Organization-wide coding standards distributed as a package
- **Framework skills**: Best practices for specific frameworks (e.g., Azure, EF Core)
- **Team skills**: Shared knowledge across team projects
- **MCP servers**: Ship MCP server configs alongside skills — consumers get both AI knowledge and tool access from a single NuGet install
- **Library + Skills**: Ship a utility library with AI guidance on how to use it


Library authors can choose if Skills and MCP fragments are opt-in or opt-out for consumers. By setting `ImprintEnabledByDefault` in the package's `.csproj`, authors control the default behavior:

```xml
<PropertyGroup>
  <ImprintEnabledByDefault>false</ImprintEnabledByDefault> <!-- Opt-in: disabled unless user enables -->
</PropertyGroup>
```

Consumers can always override this per-package using metadata on their `PackageReference`:

```xml
<PackageReference Include="SomePackage" Version="1.0.0">
  <ImprintEnabled>false</ImprintEnabled> <!-- Disable or enable this package's skills/MCP -->
</PackageReference>
```

The consumer's explicit setting always takes priority over the package author's default.

## Quick Start

### Creating Your Own Imprint Package

Create a new class library project and add `<Imprint>` items to declare your content:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Zakira.Imprint.Sdk" Version="1.0.0-preview" />
  </ItemGroup>

  <!-- Declare your content using <Imprint> items -->
  <ItemGroup>
    <Imprint Include="skills\**\*" /> 
    <Imprint Include="mcp\*.mcp.json" Type="Mcp" />             <!-- MCP server configs -->
  </ItemGroup>
</Project>
```

Create your skills in a `skills/` folder, then pack:

```bash
dotnet pack -o ./packages
```

### Consuming an Imprint Package

```bash
# Add the package
dotnet add package <some-Imprint-package>

# Build to install skills (happens automatically before build)
dotnet build

# Skills are now at .github/skills/, .claude/skills/, .cursor/rules/ etc.
```

Imprint auto-detects which AI agents you use by scanning for their configuration directories (`.github/`, `.claude/`, `.cursor/`). Skills are copied to each detected agent's native location.

A shared `.gitignore` is automatically generated at `.imprint/.gitignore`, so no manual `.gitignore` configuration is needed.

The SDK automatically generates the `.targets` file at pack time — no manual MSBuild authoring required!

## Multi-Agent Support

Imprint includes multi-agent support. Instead of targeting only GitHub Copilot, Imprint can distribute skills and MCP configurations to **multiple AI agents simultaneously**, placing files in each agent's native directory structure.

### Supported Agents

| Agent | Detection | Skills Path | MCP Path | MCP Root Key |
|-------|-----------|-------------|----------|--------------|
| `copilot` | `.github/` exists | `.github/skills/` | `.vscode/mcp.json` | `servers` |
| `claude` | `.claude/` exists | `.claude/skills/` | `.claude/mcp.json` | `mcpServers` |
| `cursor` | `.cursor/` exists | `.cursor/rules/` | `.cursor/mcp.json` | `mcpServers` |
| `roo` | `.roo/` exists | `.roo/rules/` | `.roo/mcp.json` | `mcpServers` |
| `opencode` | `.opencode/` exists | `.opencode/skills/` | `opencode.json` (project root) | `mcp` |

Unknown agent names fall back to `.{name}/skills/` for skills and `.{name}/mcp.json` for MCP.

### Agent Resolution

Imprint determines which agents to target using a priority hierarchy:

1. **Explicit configuration** — Set `ImprintTargetAgents` in your `.csproj`:
   ```xml
   <PropertyGroup>
     <ImprintTargetAgents>claude;cursor</ImprintTargetAgents>
   </PropertyGroup>
   ```

2. **Auto-detection** (default, ON) — Scans for agent directories at build time. If `.github/` and `.claude/` exist, both `copilot` and `claude` are targeted.

3. **Default fallback** — If no directories are detected:
   ```xml
   <PropertyGroup>
     <ImprintDefaultAgents>copilot</ImprintDefaultAgents>
   </PropertyGroup>
   ```

### Configuration Properties

| Property | Default | Purpose |
|----------|---------|---------|
| `ImprintTargetAgents` | *(empty)* | Explicit agent list (semicolon-separated). Overrides auto-detection. |
| `ImprintAutoDetectAgents` | `true` | Scan for agent directories at build time |
| `ImprintDefaultAgents` | `copilot` | Fallback when no agents are detected |

### Example Output

With `.github/` and `.claude/` directories present, installing `Zakira.Imprint.Sample` produces:

```
.github/
  skills/
    personal/
      SKILL.md              # Copilot sees this
.claude/
  skills/
    personal/
      SKILL.md              # Claude sees this
.vscode/
  mcp.json                  # MCP servers for Copilot/VS Code
.claude/
  mcp.json                  # MCP servers for Claude
.imprint/
  manifest.json             # Unified tracking manifest (v2)
  .gitignore                # Prevents tracking of managed files
```

## Available Packages

| Package | Version | Description |
|---------|---------|-------------|
| **Zakira.Imprint.Sdk** | 1.0.0-preview | Core MSBuild task engine — auto-generates `.targets`, content copying, cleaning, MCP merging, multi-agent support |

## MCP Server Injection

Imprint packages can ship MCP (Model Context Protocol) server configurations. When you build, server configs are automatically merged into each targeted agent's `mcp.json`.

### How MCP Injection Works

1. Each Imprint package includes a `mcp/<PackageId>.mcp.json` fragment file containing its server definitions
2. At build time, `Zakira.Imprint.Sdk` collects all `ImprintMcpFragment` items from installed packages
3. For each resolved agent, servers are merged into that agent's `mcp.json`, preserving any servers you've configured manually
4. The unified manifest tracks which servers are managed by Imprint
5. On `dotnet clean`, only Imprint-managed servers are removed — your servers are never touched

### Example Fragment File

An Imprint package's `mcp/<PackageId>.mcp.json`:

```json
{
  "servers": {
    "sample-echo-server": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@anthropic-ai/echo-mcp-server"]
    }
  }
}
```

> **Note**: Package authors always use `"servers"` as the root key in fragment files. The SDK automatically transforms this to the correct root key for each agent when writing to their `mcp.json` files.

After `dotnet build` with both `copilot` and `claude` agents detected:

- `.vscode/mcp.json` contains the server under `"servers"` (VS Code / Copilot schema)
- `.claude/mcp.json` contains the server under `"mcpServers"` (Claude schema)

### Key Behaviors

- **Idempotent**: If nothing changed, `mcp.json` is not rewritten (no git noise)
- **Safe clean**: `dotnet clean` removes only managed servers; if no user servers remain, `mcp.json` is deleted
- **User servers preserved**: Any servers you add manually to `mcp.json` are never modified or removed
- **`inputs` preserved**: Top-level properties like `"inputs"` in `mcp.json` are preserved through builds and cleans
- **Multi-agent**: Each agent gets its own `mcp.json` in its native location
- **Schema transformation**: The SDK automatically uses the correct root key per agent (`"servers"` for Copilot, `"mcpServers"` for Claude/Cursor)


### Adding MCP Injection to Your Package

1. Add a `PackageReference` to `Zakira.Imprint.Sdk` in your `.csproj`

2. Create a `mcp/<YourPackageId>.mcp.json` fragment with your server definitions

3. Add the fragment using the `<Imprint>` item with `Type="Mcp"`:

```xml
<ItemGroup>
  <Imprint Include="skills\**\*" />                           <!-- Skills -->
  <Imprint Include="mcp\*.mcp.json" Type="Mcp" />             <!-- MCP fragments -->
</ItemGroup>
```

The SDK auto-generates the `.targets` file at pack time — no manual configuration needed!

## Two Package Patterns

### Skills-Only Package

For packages that only distribute AI skills and MCP configs (no compiled code):

```xml
<PropertyGroup>
  <IncludeBuildOutput>false</IncludeBuildOutput>
  <DevelopmentDependency>true</DevelopmentDependency>
</PropertyGroup>
```

See `samples/Sample.FilesOnly` for an example.

### Code + Skills Package

For packages that ship both a compiled DLL **and** AI skills, you do not need to set any special properties — just include your code files and `<Imprint>` items together. The DLL will be included as usual, and skills/MCP configs will be distributed to agents.

## Configuration

### Agent Targeting

Control which AI agents Imprint targets:

```xml
<PropertyGroup>
  <!-- Target specific agents (overrides auto-detection) -->
  <ImprintTargetAgents>copilot;claude</ImprintTargetAgents>

  <!-- Or disable auto-detection and use only defaults -->
  <ImprintAutoDetectAgents>false</ImprintAutoDetectAgents>
  <ImprintDefaultAgents>copilot</ImprintDefaultAgents>
</PropertyGroup>
```

## License

MIT

