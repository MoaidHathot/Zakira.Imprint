<p align="center">
  <img src="assets/Zakira.Imprint.icon.png" alt="Zakira.Imprint Logo" width="128" height="128" />
</p>

# Imprint

**Distribute AI Skills and MCP configurations via NuGet packages**

[![Documentation](https://img.shields.io/badge/docs-moaidhathot.github.io%2FZakira.Imprint-blue)](https://moaidhathot.github.io/Zakira.Imprint/)

## Overview

Imprint is a pattern for distributing AI Skills (those `SKILLS.md` files for GitHub Copilot, Claude, Cursor, Roo Code, and other AI assistants) and MCP Server configuration via NuGet packages. When you add an Imprint package to your project:

1. **On `dotnet build`**: Skills are automatically copied to each AI agent's native directory
2. **On `dotnet clean`**: Skills are removed (including empty parent directories)
3. **Multi-agent support**: Targets Copilot, Claude, Cursor, Roo Code, OpenCode, and Windsurf simultaneously — each gets files in its native location (if exists)
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

### Consuming an Imprint Package

```bash
# Add the package
dotnet add package <some-Imprint-package>

# Build to install skills (happens automatically before build)
dotnet build

# Skills are now at .github/skills/, .claude/skills/, .cursor/rules/, .roo/rules/ etc.
```

Imprint auto-detects which AI agents you use by scanning for their configuration directories (`.github/`, `.claude/`, `.cursor/`, `.roo/`, `.opencode/`, `.windsurf/`). Skills are copied to each detected agent's native location.

A shared `.gitignore` is automatically generated at `.imprint/.gitignore`, so no manual `.gitignore` configuration is needed.

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
| `windsurf` | `.windsurf/` exists | `.windsurf/rules/` | `.windsurf/mcp.json` | `mcpServers` |
| `agents` | `.agents/` exists | `.agents/skills/` | `.agents/mcp.json` | `mcpServers` |

Unknown agent names fall back to `.{name}/rules/` for skills and `.{name}/mcp.json` for MCP.

### Agent Resolution

Imprint determines which agents to target using a priority hierarchy:

1. **Explicit configuration** — Set `ImprintTargetAgents` in your `.csproj`:
   ```xml
   <PropertyGroup>
     <ImprintTargetAgents>claude;cursor</ImprintTargetAgents>
   </PropertyGroup>
   ```

2. **Auto-detection** (default, ON) — Scans for agent directories at build time. If `.github/` and `.claude/` exist, both `copilot` and `claude` are targeted.
   Supported detection directories: `.github/` (copilot), `.claude/` (claude), `.cursor/` (cursor), `.roo/` (roo), `.opencode/` (opencode), `.windsurf/` (windsurf), `.agents/` (agents).

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
| `ImprintDefaultAgents` | *(empty)* | Fallback when no agents are detected |
| `ImprintRootDirectory` | *(auto-detected)* | Repository root where skills are placed. See [Root Directory](#root-directory). |

### Root Directory

By default, Imprint auto-detects the **repository root** by walking up from the project directory. Markers are checked in priority order:

1. **VCS directories** - `.git/`, `.svn/`, `.hg/` (most authoritative)
2. **IDE directories** - `.vs/` (Visual Studio), `.idea/` (JetBrains)
3. **Solution files** - `*.sln`, `*.slnx` (fallback)

This ensures that in multi-project solutions, skills are placed at the repository root (where `.git/` lives) rather than inside individual project directories:

```
/repo-root/              <-- Skills placed here (correct)
├── .git/
├── .github/skills/      <-- Not inside src/MyProject/.github/
├── src/
│   └── MyProject/
│       └── MyProject.csproj
└── MyApp.sln
```

If auto-detection doesn't work for your setup, you can explicitly set the root directory:

```xml
<PropertyGroup>
  <ImprintRootDirectory>$(MSBuildThisFileDirectory)..\</ImprintRootDirectory>
</PropertyGroup>
```

If no repository root is found and no explicit override is set, Imprint falls back to the project directory.

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

## How It Works

### Architecture

All Imprint skill packages depend on **Zakira.Imprint.Sdk**, which provides the MSBuild task engine. Package authors declare `<Imprint>` items in their `.csproj` — the SDK auto-generates the `.targets` file at pack time, then handles agent resolution, file copying, MCP merging, manifest tracking, and cleanup at build/clean time.

```
┌───────────────────────────┐  ┌────────────────────────────────────┐
│  Sample                   │  │  Sample.FilesOnly                  │
│  (skills + MCP + code)    │  │  (skills-only)                     │
└──────┬────────────────────┘  └──────┬─────────────────────────────┘
       │                              │
       ▼                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Zakira.Imprint.Sdk                                                 │
│  - Auto-generates .targets at pack time (ImprintGenerateTargets)    │
│  - Copies skills to all agents (ImprintCopyContent)                 │
│  - Merges MCP servers (ImprintMergeMcpServers)                      │
│  - Cleans on dotnet clean (ImprintCleanContent, ImprintCleanMcp)    │
└─────────────────────────────────────────────────────────────────────┘
```

### Build-Time Flow

1. **NuGet Restore**: NuGet restores skill packages, which transitively pull in `Zakira.Imprint.Sdk`. MSBuild auto-imports the SDK's props and targets via the `buildTransitive/` folder.

2. **Agent Resolution**: Before any file operations, `AgentConfig.ResolveAgents()` determines which agents to target:
   - If `ImprintTargetAgents` is set, use that explicit list
   - Else if `ImprintAutoDetectAgents` is true, scan for `.github/`, `.claude/`, `.cursor/`, `.roo/`, `.opencode/`, `.windsurf/` directories
   - Else fall back to `ImprintDefaultAgents`

3. **Content Copy** (`Imprint_CopyContent`): For each resolved agent, copies skill files to the agent's native skills directory. Writes a unified manifest v2 at `.imprint/manifest.json` tracking all files per-agent per-package. These copies and the shared `.gitignore`/manifest writes are safe under parallel, multi-project builds: destinations that already match the source are skipped, and concurrent writers are retried with backoff and reconciled, so building a solution with `-m` will not fail with "file is being used by another process" errors.

4. **MCP Merge** (`Imprint_MergeMcp`): Merges MCP server fragments into each agent's `mcp.json`. Tracks managed server keys in the unified manifest.

5. **Clean** (`Imprint_CleanContent` + `Imprint_CleanMcp`): Reads the unified manifest to delete only tracked files and managed MCP servers. Removes empty directories. Preserves user-defined MCP servers.

### Pack-Time Flow

When you run `dotnet pack` on your Imprint package:

1. **Generate Targets** (`Imprint_GenerateTargetsFile`): The SDK reads all `<Imprint>` items from your `.csproj` and generates a `.targets` file at `obj/{Configuration}/{TFM}/Imprint/{PackageId}.targets`. This file declares `ImprintContent` and `ImprintMcpFragment` items that consumers will use.

2. **Include Content + Cross-Targeting Import** (`Imprint_IncludeContentInPackage`): The SDK adds the generated `.targets` file to both `build/` and `buildTransitive/` package paths, includes all content files (skills, MCP fragments) in the `content/` folder, and for multi-targeted package projects (`<TargetFrameworks>...`) imports via `buildMultiTargeting/` in the outer pack build so Imprint pack targets can run before `_GetPackageFiles`.

3. **NuGet Pack**: The package is created with all necessary files — no manual `.targets` authoring required.

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

### MCP Schema Differences

Different AI agents use different JSON schemas for their MCP configuration files. The SDK handles this automatically:

| Agent | Root Key | Example |
|-------|----------|---------|
| Copilot (VS Code) | `servers` | `{"servers": {"my-server": {...}}}` |
| Claude | `mcpServers` | `{"mcpServers": {"my-server": {...}}}` |
| Cursor | `mcpServers` | `{"mcpServers": {"my-server": {...}}}` |
| Roo Code | `mcpServers` | `{"mcpServers": {"my-server": {...}}}` |
| OpenCode | `mcp` | `{"mcp": {"my-server": {...}}}` |
| Windsurf | `mcpServers` | `{"mcpServers": {"my-server": {...}}}` |

**Package authors always write fragments using `"servers"`** as the root key. The SDK reads these fragments and transforms them to each agent's expected schema when writing to their respective `mcp.json` files. The inner server definition (`command`, `args`, `type`, `env`) is identical across all agents.

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

For packages that ship both a compiled DLL **and** AI skills:

```xml
<PropertyGroup>
  <!-- IncludeBuildOutput defaults to true - DLL ships in lib/ -->
  <!-- Do NOT set DevelopmentDependency - consumers need the runtime DLL -->
</PropertyGroup>
```

See `samples/Sample` for an example — it ships string utility methods alongside an AI skill file and MCP server configuration.

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
# 1. Pack the SDK first, then samples (to local-packages/)
dotnet pack src/Zakira.Imprint.Sdk -o ./local-packages
dotnet pack samples/Sample -o ./local-packages
dotnet pack samples/Sample.FilesOnly -o ./local-packages

# 2. Create a test consumer (or use samples/Consumer)
cd samples/Consumer
dotnet build

# 3. Verify skills are installed (agent directories vary by your setup)
ls .github/skills/
# personal/  StringUtils/

# 4. Verify MCP servers were injected
cat .vscode/mcp.json
# { "servers": { "sample-echo-server": {...} } }

# 5. Run unit tests
cd ../..
dotnet test Zakira.Imprint.sln

# 6. Test clean - skills and managed MCP servers are removed
cd samples/Consumer
dotnet clean
ls .github/          # Should be empty or not exist
ls .vscode/mcp.json  # Should not exist (no user servers to preserve)

# 7. Build again - everything is restored
dotnet build
```

## Limitations & Known Issues

1. **Package Removal**: When you remove an Imprint package, its skills remain until you run `dotnet clean` or manually delete them.

2. **IDE Design-Time Builds**: Skills and MCP servers are only managed during actual builds, not during IDE background builds (this is intentional to avoid performance issues).

3. **First Build Required**: Skills and MCP configs are installed on the first build, not on restore.

4. **Shared Output Folder**: Multiple packages write to the same skills directory per agent. If two packages include a file with the same relative path, the last one to copy wins.

5. **MCP Server Key Conflicts**: If two Imprint packages define a server with the same key, the last fragment processed wins silently. A warning for this is planned.

6. **Zakira.Imprint.Sdk requires .NET 8+**: The Zakira.Imprint.Sdk compiled task DLL targets `net8.0`. Consumers must have the .NET 8 SDK or later installed.

## Future Enhancements

- [x] ~~Multiple skills packages in one project~~
- [x] ~~MCP Server Injection~~
- [x] ~~Centralized SDK (Zakira.Imprint.Sdk) — single source for all MSBuild logic~~
- [x] ~~Per-package manifests for precise file tracking~~
- [x] ~~Code + Skills package pattern~~
- [x] ~~Unit tests for MSBuild task classes~~
- [x] ~~Multi-agent support (Copilot, Claude, Cursor, Roo Code)~~
- [x] ~~Auto-detection of AI agents~~
- [x] ~~Unified manifest v2 with per-agent tracking~~
- [x] ~~Auto-generated `.targets` files — no manual MSBuild authoring required~~
- [x] ~~Repository root auto-detection for multi-project solutions~~
- [ ] Server key conflict detection/warnings when multiple packages define the same key
- [ ] Global tool for managing skills across solutions
- [ ] Skill validation during pack
- [ ] Conflict detection between skill packages
- [ ] CI/CD pipeline for building and publishing packages
- [ ] Prompts support (distribute `.prompt` files to agent-specific directories)
- [x] ~~Windsurf agent support~~
- [ ] Additional agent support (Cody, etc.)

## License

MIT
