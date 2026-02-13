# Imprint

**Distribute AI Skills via NuGet packages** - like Roslyn Analyzers, but for AI assistants.

## Overview

Imprint is a pattern for distributing AI Skills (those `SKILLS.md` files for GitHub Copilot, Claude, Cursor, and other AI assistants) via NuGet packages. When you add an Imprint package to your project:

1. **On `dotnet build`**: Skills are automatically copied to each AI agent's native directory
2. **On `dotnet clean`**: Skills are removed (including empty parent directories)
3. **Multi-agent support**: Targets Copilot, Claude, and Cursor simultaneously — each gets files in its native location
4. **All file types supported**: Not just `.md` — scripts, configs, and any other files in the `skills/` folder are included
5. **MCP Server Injection**: Packages can inject [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server configurations into each agent's `mcp.json`
6. **Code + Skills**: Packages can ship both a compiled DLL library **and** AI skills — consumers get runtime APIs and AI guidance from a single NuGet install

This enables scenarios like:
- **Compliance skills**: Organization-wide coding standards distributed as a package
- **Framework skills**: Best practices for specific frameworks (e.g., Azure, EF Core)
- **Team skills**: Shared knowledge across team projects
- **MCP servers**: Ship MCP server configs alongside skills — consumers get both AI knowledge and tool access from a single NuGet install
- **Library + Skills**: Ship a utility library with AI guidance on how to use it

## Quick Start

### Consuming an Imprint Package

```bash
# Add the package
dotnet add package Zakira.Imprint.AzureBestPractices

# Build to install skills (happens automatically before build)
dotnet build

# Skills are now at .github/skills/, .claude/skills/, .cursor/rules/ etc.
```

Imprint auto-detects which AI agents you use by scanning for their configuration directories (`.github/`, `.claude/`, `.cursor/`). Skills are copied to each detected agent's native location.

A shared `.gitignore` is automatically generated at `.imprint/.gitignore`, so no manual `.gitignore` configuration is needed.

### Creating Your Own Imprint Package

The easiest way is to use the template:

```bash
# Install the template
dotnet new install Zakira.Imprint.Template

# Create a new skills package
mkdir MyOrg.CodingStandards
cd MyOrg.CodingStandards
dotnet new imprint -n MyOrg.CodingStandards

# Edit skills/SKILL-Example.md with your actual content
# Then pack and publish
dotnet pack -o ./packages
```

## Multi-Agent Support

Imprint v1.1.0 introduced multi-agent support. Instead of targeting only GitHub Copilot, Imprint can now distribute skills and MCP configurations to **multiple AI agents simultaneously**, placing files in each agent's native directory structure.

### Supported Agents

| Agent | Detection | Skills Path | MCP Path |
|-------|-----------|-------------|----------|
| `copilot` | `.github/` exists | `.github/skills/` | `.vscode/mcp.json` |
| `claude` | `.claude/` exists | `.claude/skills/` | `.claude/mcp.json` |
| `cursor` | `.cursor/` exists | `.cursor/rules/` | `.cursor/mcp.json` |

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
| **Zakira.Imprint.Sdk** | 1.1.0 | Core MSBuild task engine — content copying, cleaning, MCP merging, multi-agent support |
| **Zakira.Imprint.Sample** | 1.2.0 | Basic example with a single demo skill + sample MCP server |
| **Zakira.Imprint.AzureBestPractices** | 1.2.0 | Azure SDK usage, naming conventions, security practices + Azure MCP server |
| **Zakira.Imprint.Sample.WithCode** | 1.0.0 | Example of shipping both a compiled DLL (string utilities) **and** AI skills from a single package |
| **Zakira.Imprint.Template** | 1.0.0 | `dotnet new` template for creating your own Imprint packages |

## Project Structure

```
Imprint/
├── Zakira.Imprint.sln
├── nuget.config                          # Solution-level config, local package feed
├── src/
│   ├── Zakira.Imprint.Sdk/               # Core MSBuild task engine (v1.1.0)
│   │   ├── Zakira.Imprint.Sdk.csproj
│   │   ├── AgentConfig.cs               # Multi-agent definitions & resolution
│   │   ├── ImprintCopyContent.cs        # MSBuild Task: copy skills to all agents
│   │   ├── ImprintCleanContent.cs       # MSBuild Task: clean skills on dotnet clean
│   │   ├── ImprintMergeMcpServers.cs    # MSBuild Task: merge MCP fragments per agent
│   │   ├── ImprintCleanMcpServers.cs    # MSBuild Task: clean managed MCP servers
│   │   ├── build/
│   │   │   ├── Zakira.Imprint.Sdk.props       # Default property values (agent settings)
│   │   │   └── Zakira.Imprint.Sdk.targets     # UsingTask + target definitions
│   │   └── buildTransitive/
│   │       ├── Zakira.Imprint.Sdk.props
│   │       └── Zakira.Imprint.Sdk.targets     # Enables transitive consumers
│   │
│   ├── Zakira.Imprint.Sample/            # Example skills-only package (v1.2.0)
│   │   ├── Zakira.Imprint.Sample.csproj
│   │   ├── build/
│   │   │   └── Zakira.Imprint.Sample.targets
│   │   ├── mcp/
│   │   │   └── Zakira.Imprint.Sample.mcp.json
│   │   └── skills/
│   │       └── personal/
│   │           └── SKILL.md
│   │
│   ├── Zakira.Imprint.AzureBestPractices/      # Azure best practices skills (v1.2.0)
│   │   ├── Zakira.Imprint.AzureBestPractices.csproj
│   │   ├── build/
│   │   │   └── Zakira.Imprint.AzureBestPractices.targets
│   │   ├── mcp/
│   │   │   └── Zakira.Imprint.AzureBestPractices.mcp.json
│   │   └── skills/
│   │       └── SKILL-AzureSecurity.md
│   │
│   ├── Zakira.Imprint.Sample.WithCode/   # DLL + skills package (v1.0.0)
│   │   ├── Zakira.Imprint.Sample.WithCode.csproj
│   │   ├── StringExtensions.cs           # String extension methods
│   │   ├── StringHelper.cs               # Static string helpers
│   │   ├── build/
│   │   │   └── Zakira.Imprint.Sample.WithCode.targets
│   │   ├── mcp/
│   │   │   └── Zakira.Imprint.Sample.WithCode.mcp.json
│   │   └── skills/
│   │       └── SKILL-StringUtils.md
│   │
│   └── ConsumerProject/                  # Test consumer
│       ├── ConsumerProject.csproj
│       └── Program.cs
│
├── templates/
│   └── Zakira.Imprint.Template/          # dotnet new template
│       ├── Zakira.Imprint.Template.csproj
│       └── content/
│           ├── .template.config/
│           │   └── template.json
│           ├── Zakira.Imprint.MySkills.csproj
│           ├── build/
│           │   └── Zakira.Imprint.MySkills.targets
│           ├── mcp/
│           │   └── Zakira.Imprint.MySkills.mcp.json
│           └── skills/
│               └── SKILL-Example.md
│
├── tests/
│   ├── Zakira.Imprint.Sdk.Tests/         # SDK unit tests (113 tests)
│   │   ├── Zakira.Imprint.Sdk.Tests.csproj
│   │   ├── MockBuildEngine.cs
│   │   ├── MockTaskItem.cs
│   │   ├── AgentConfigTests.cs           # 38 tests — agent resolution logic
│   │   ├── MultiAgentTests.cs            # 28 tests — multi-agent workflows
│   │   ├── ImprintCopyContentTests.cs
│   │   ├── ImprintCleanContentTests.cs
│   │   ├── ImprintMergeMcpServersTests.cs
│   │   └── ImprintCleanMcpServersTests.cs
│   │
│   └── Zakira.Imprint.Sample.WithCode.Tests/   # WithCode library tests (84 tests)
│       ├── Zakira.Imprint.Sample.WithCode.Tests.csproj
│       ├── StringExtensionsTests.cs
│       └── StringHelperTests.cs
│
├── packages/                             # Local package output
└── README.md
```

## How It Works

### Architecture

All Imprint skill packages depend on **Zakira.Imprint.Sdk**, which provides the MSBuild task engine. Skill packages only need a ~25-line `.targets` file that declares what content to ship — the SDK handles agent resolution, file copying, MCP merging, manifest tracking, and cleanup.

```
┌───────────────────────────┐  ┌────────────────────────────────────┐
│  Zakira.Imprint.Sample    │  │  Zakira.Imprint.AzureBestPractices │
│  (skills-only)            │  │  (skills-only)                     │
└──────┬────────────────────┘  └──────┬─────────────────────────────┘
       │                              │
       │   ┌─────────────────────────────────────────┐
       │   │  Zakira.Imprint.Sample.WithCode         │
       │   │  (DLL + skills)                         │
       │   └──────┬──────────────────────────────────┘
       │          │
       ▼          ▼
┌─────────────────────────────┐
│  Zakira.Imprint.Sdk         │
│  (MSBuild tasks: copy,     │
│   clean, MCP merge/clean,  │
│   multi-agent resolution)  │
└─────────────────────────────┘
```

### Build-Time Flow

1. **NuGet Restore**: NuGet restores skill packages, which transitively pull in `Zakira.Imprint.Sdk`. MSBuild auto-imports the SDK's props and targets via the `buildTransitive/` folder.

2. **Agent Resolution**: Before any file operations, `AgentConfig.ResolveAgents()` determines which agents to target:
   - If `ImprintTargetAgents` is set, use that explicit list
   - Else if `ImprintAutoDetectAgents` is true, scan for `.github/`, `.claude/`, `.cursor/` directories
   - Else fall back to `ImprintDefaultAgents` (default: `copilot`)

3. **Content Copy** (`Imprint_CopyContent`): For each resolved agent, copies skill files to the agent's native skills directory. Writes a unified manifest v2 at `.imprint/manifest.json` tracking all files per-agent per-package.

4. **MCP Merge** (`Imprint_MergeMcp`): Merges MCP server fragments into each agent's `mcp.json`. Tracks managed server keys in the unified manifest.

5. **Clean** (`Imprint_CleanContent` + `Imprint_CleanMcp`): Reads the unified manifest to delete only tracked files and managed MCP servers. Removes empty directories. Preserves user-defined MCP servers.

### Unified Manifest (v2)

Imprint uses a single manifest at `.imprint/manifest.json` to track everything:

```json
{
  "version": 2,
  "packages": {
    "Zakira.Imprint.Sample": {
      "files": {
        "copilot": [".github/skills/personal/SKILL.md"],
        "claude": [".claude/skills/personal/SKILL.md"]
      }
    }
  },
  "mcp": {
    "copilot": {
      "path": ".vscode/mcp.json",
      "managedServers": ["sample-echo-server"]
    },
    "claude": {
      "path": ".claude/mcp.json",
      "managedServers": ["sample-echo-server"]
    }
  }
}
```

Legacy per-package `.manifest` files are still written for backward compatibility.

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

After `dotnet build` with both `copilot` and `claude` agents detected:

- `.vscode/mcp.json` contains the server (for VS Code / Copilot)
- `.claude/mcp.json` contains the same server (for Claude)

### Key Behaviors

- **Idempotent**: If nothing changed, `mcp.json` is not rewritten (no git noise)
- **Safe clean**: `dotnet clean` removes only managed servers; if no user servers remain, `mcp.json` is deleted
- **User servers preserved**: Any servers you add manually to `mcp.json` are never modified or removed
- **`inputs` preserved**: Top-level properties like `"inputs"` in `mcp.json` are preserved through builds and cleans
- **Multi-agent**: Each agent gets its own `mcp.json` in its native location

### Adding MCP Injection to Your Package

1. Add a `PackageReference` to `Zakira.Imprint.Sdk` in your `.csproj`:

```xml
<PackageReference Include="Zakira.Imprint.Sdk" Version="1.1.0">
  <PrivateAssets>compile</PrivateAssets>
</PackageReference>
```

2. Create a `mcp/<YourPackageId>.mcp.json` fragment with your server definitions.

3. Add the fragment to the `ImprintMcpFragment` item group in your `.targets` file:

```xml
<ItemGroup>
  <ImprintMcpFragment Include="$(_YourPkg_Root)mcp\*.mcp.json" />
</ItemGroup>
```

4. Include the `mcp/` directory in your NuGet package:

```xml
<None Include="mcp\**" Pack="true" PackagePath="mcp" />
```

## Two Package Patterns

### Skills-Only Package

For packages that only distribute AI skills and MCP configs (no compiled code):

```xml
<PropertyGroup>
  <IncludeBuildOutput>false</IncludeBuildOutput>
  <DevelopmentDependency>true</DevelopmentDependency>
</PropertyGroup>
```

See `Zakira.Imprint.Sample` and `Zakira.Imprint.AzureBestPractices` for examples.

### Code + Skills Package

For packages that ship both a compiled DLL **and** AI skills:

```xml
<PropertyGroup>
  <!-- IncludeBuildOutput defaults to true - DLL ships in lib/ -->
  <!-- Do NOT set DevelopmentDependency - consumers need the runtime DLL -->
</PropertyGroup>

<PackageReference Include="Zakira.Imprint.Sdk" Version="1.1.0">
  <PrivateAssets>compile</PrivateAssets>
</PackageReference>
```

See `Zakira.Imprint.Sample.WithCode` for a complete example — it ships string utility methods (Slugify, Truncate, Mask, etc.) alongside an AI skill file that teaches AI assistants how to use the API.

## Using the Template

### Installation

```bash
# From NuGet (when published)
dotnet new install Zakira.Imprint.Template

# From local package
dotnet new install ./packages/Zakira.Imprint.Template.1.0.0.nupkg
```

### Creating a New Package

```bash
# Basic usage
dotnet new imprint -n MyOrg.Skills

# With all options
dotnet new imprint -n MyOrg.Skills \
  --description "My organization's coding standards" \
  --version 1.0.0
```

### Template Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-n, --name` | Package ID and project name | `Zakira.Imprint.MySkills` |
| `--description` | Package description | `AI Skills for your domain.` |
| `--version` | Initial package version | `1.0.0` |

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

### Legacy Path Overrides

These properties are still available for backward compatibility but are generally superseded by multi-agent resolution:

| Property | Default | Purpose |
|----------|---------|---------|
| `ImprintSkillsPath` | `.github/skills/` | Legacy: single-agent skills path |
| `ImprintPromptsPath` | `.github/prompts/` | Legacy: single-agent prompts path |
| `ImprintMcpPath` | `.vscode/` | Legacy: single-agent MCP path |

## Testing This Repo

```bash
# 1. Pack all packages (Zakira.Imprint.Sdk must be packed first)
dotnet pack src/Zakira.Imprint.Sdk -o ./packages
dotnet pack src/Zakira.Imprint.Sample -o ./packages
dotnet pack src/Zakira.Imprint.AzureBestPractices -o ./packages
dotnet pack src/Zakira.Imprint.Sample.WithCode -o ./packages

# 2. Build consumer - skills are installed to all detected agents
cd src/ConsumerProject
dotnet build

# 3. Verify skills are installed (agent directories vary by your setup)
ls .github/skills/
# .gitignore  personal/  SKILL-AzureSecurity.md  SKILL-StringUtils.md

# 4. Verify MCP servers were injected
cat .vscode/mcp.json
# { "servers": { "sample-echo-server": {...}, "azure-mcp-server": {...}, ... } }

# 5. Run unit tests (197 total)
cd ../..
dotnet test Zakira.Imprint.sln

# 6. Test clean - skills and managed MCP servers are removed
cd src/ConsumerProject
dotnet clean
ls .github/          # Should be empty or not exist
ls .vscode/mcp.json  # Should not exist (no user servers to preserve)

# 7. Build again - everything is restored
dotnet build
```

## Manual Package Creation

If you prefer not to use the template, create a package manually:

1. **Create project structure:**
```
MyOrg.Skills/
├── MyOrg.Skills.csproj
├── build/
│   └── MyOrg.Skills.targets
├── mcp/
│   └── MyOrg.Skills.mcp.json          # Optional: MCP server config
├── skills/
│   └── SKILL-YourSkill.md
└── README.md
```

2. **Configure the `.csproj`:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <PackageId>MyOrg.Skills</PackageId>
    <Version>1.0.0</Version>
    
    <!-- This is a tools-only package -->
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
    <NoPackageAnalysis>true</NoPackageAnalysis>
    <SuppressDependenciesWhenPacking>false</SuppressDependenciesWhenPacking>
  </PropertyGroup>

  <!-- Reference Zakira.Imprint.Sdk for content copy + MCP merge logic -->
  <ItemGroup>
    <PackageReference Include="Zakira.Imprint.Sdk" Version="1.1.0">
      <PrivateAssets>compile</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <None Include="build\**" Pack="true" PackagePath="build" />
    <None Include="skills\**\*" Pack="true" PackagePath="skills" />
    <None Include="mcp\**" Pack="true" PackagePath="mcp" />
  </ItemGroup>
</Project>
```

3. **Create the `.targets` file** (`build/MyOrg.Skills.targets`):
```xml
<Project>
  <PropertyGroup>
    <_MyOrg_Skills_Root>$(MSBuildThisFileDirectory)..\</_MyOrg_Skills_Root>
  </PropertyGroup>

  <ItemGroup>
    <ImprintContent Include="$(_MyOrg_Skills_Root)skills\**\*">
      <DestinationBase>$(ImprintSkillsPath)</DestinationBase>
      <PackageId>MyOrg.Skills</PackageId>
      <SourceBase>$(_MyOrg_Skills_Root)skills\</SourceBase>
    </ImprintContent>
  </ItemGroup>

  <ItemGroup>
    <ImprintMcpFragment Include="$(_MyOrg_Skills_Root)mcp\*.mcp.json" />
  </ItemGroup>
</Project>
```

## Limitations & Known Issues

1. **Package Removal**: When you remove an Imprint package, its skills remain until you run `dotnet clean` or manually delete them.

2. **IDE Design-Time Builds**: Skills and MCP servers are only managed during actual builds, not during IDE background builds (this is intentional to avoid performance issues).

3. **First Build Required**: Skills and MCP configs are installed on the first build, not on restore.

4. **Shared Output Folder**: Multiple packages write to the same skills directory per agent. If two packages include a file with the same relative path, the last one to copy wins.

5. **MCP Server Key Conflicts**: If two Imprint packages define a server with the same key, the last fragment processed wins silently. A warning for this is planned.

6. **Zakira.Imprint.Sdk requires .NET 8+**: The Zakira.Imprint.Sdk compiled task DLL targets `net8.0`. Consumers must have the .NET 8 SDK or later installed.

## Future Enhancements

- [x] ~~dotnet new template for creating packages~~
- [x] ~~Multiple skills packages in one project~~
- [x] ~~MCP Server Injection~~
- [x] ~~Centralized SDK (Zakira.Imprint.Sdk) — single source for all MSBuild logic~~
- [x] ~~Per-package manifests for precise file tracking~~
- [x] ~~Code + Skills package pattern (Zakira.Imprint.Sample.WithCode)~~
- [x] ~~Unit tests for MSBuild task classes (197 tests)~~
- [x] ~~Multi-agent support (Copilot, Claude, Cursor)~~
- [x] ~~Auto-detection of AI agents~~
- [x] ~~Unified manifest v2 with per-agent tracking~~
- [ ] Server key conflict detection/warnings when multiple packages define the same key
- [ ] Global tool for managing skills across solutions
- [ ] Skill validation during pack
- [ ] Conflict detection between skill packages
- [ ] CI/CD pipeline for building and publishing packages
- [ ] Prompts support (distribute `.prompt` files to agent-specific directories)
- [ ] Additional agent support (Windsurf, Cody, etc.)

## License

MIT
