# Imprint Architecture & Internals

This document provides a deep dive into how Imprint works under the hood — the MSBuild integration, agent resolution, file operations, manifest tracking, and MCP merging.

## Table of Contents

- [Overview](#overview)
- [Package Layering](#package-layering)
- [NuGet and MSBuild Integration](#nuget-and-msbuild-integration)
- [Agent Resolution](#agent-resolution)
- [Build-Time Flow](#build-time-flow)
- [Content Copy Pipeline](#content-copy-pipeline)
- [MCP Server Merge Pipeline](#mcp-server-merge-pipeline)
- [Unified Manifest (v2)](#unified-manifest-v2)
- [Clean Pipeline](#clean-pipeline)
- [Gitignore Management](#gitignore-management)
- [Two Package Patterns](#two-package-patterns)
- [Design Decisions](#design-decisions)
- [Extending Imprint](#extending-imprint)

---

## Overview

Imprint distributes AI skill files and MCP server configurations via NuGet packages. The system has three layers:

1. **Zakira.Imprint.Sdk** — The engine. Contains compiled MSBuild tasks that handle all file operations.
2. **Skill packages** — Declare what content to ship (skills and MCP fragments). Depend on Zakira.Imprint.Sdk.
3. **Consumer projects** — Reference one or more skill packages. Content is installed at build time.

The key design constraint is that skill packages should be trivial to author — a ~25-line `.targets` file and your content files. All complexity lives in the SDK.

---

## Package Layering

```
Consumer Project
    │
    ├── references Zakira.Imprint.Sample (1.2.0)
    │       └── depends on Zakira.Imprint.Sdk (1.1.0)
    │
    ├── references Zakira.Imprint.AzureBestPractices (1.2.0)
    │       └── depends on Zakira.Imprint.Sdk (1.1.0)
    │
    └── references Zakira.Imprint.Sample.WithCode (1.0.0)
            ├── ships a compiled DLL (lib/net8.0/)
            └── depends on Zakira.Imprint.Sdk (1.1.0)
```

Each skill package depends on `Zakira.Imprint.Sdk` with `<PrivateAssets>compile</PrivateAssets>`. This means:
- The SDK's build targets are imported transitively into the consumer project
- The SDK's DLL is not a runtime dependency — it is only needed at build time
- NuGet deduplicates the SDK reference if multiple skill packages are installed

---

## NuGet and MSBuild Integration

### How Targets Get Imported

NuGet has a convention: if a package contains a `build/{PackageId}.targets` file, MSBuild automatically imports it when the package is referenced. Similarly, `buildTransitive/` files are imported by transitive consumers.

Zakira.Imprint.Sdk uses both:

```
Zakira.Imprint.Sdk.nupkg
├── build/
│   ├── net8.0/Zakira.Imprint.Sdk.dll          # Compiled MSBuild tasks
│   ├── Zakira.Imprint.Sdk.props               # Default property values
│   └── Zakira.Imprint.Sdk.targets             # UsingTask + target definitions
└── buildTransitive/
    ├── Zakira.Imprint.Sdk.props               # Forwarded to transitive consumers
    └── Zakira.Imprint.Sdk.targets             # Forwarded to transitive consumers
```

The `buildTransitive/` files are critical. Without them, a consumer project that references `Zakira.Imprint.Sample` (which depends on `Zakira.Imprint.Sdk`) would not get the SDK's targets imported — NuGet only auto-imports `build/` targets for direct references by default. The `buildTransitive/` folder tells NuGet to also import for transitive (indirect) consumers.

### Import Order

MSBuild processes imports in this order during a build:

1. `Zakira.Imprint.Sdk.props` — Sets default property values (`ImprintAutoDetectAgents`, `ImprintDefaultAgents`, etc.)
2. Skill package `.targets` — Each skill package declares `ImprintContent` and `ImprintMcpFragment` items
3. `Zakira.Imprint.Sdk.targets` — Registers `UsingTask` elements and defines the four build targets

This ordering matters because:
- Props must come first so skill packages can reference properties like `$(ImprintSkillsPath)` in their item declarations
- Skill package targets must come before the SDK targets so that item collections are fully populated when the SDK's targets execute

### UsingTask Registration

The SDK targets register four compiled tasks from the `net8.0` assembly:

```xml
<UsingTask TaskName="Zakira.Imprint.Sdk.ImprintCopyContent"
           AssemblyFile="$(MSBuildThisFileDirectory)net8.0\Zakira.Imprint.Sdk.dll" />
<UsingTask TaskName="Zakira.Imprint.Sdk.ImprintMergeMcpServers"
           AssemblyFile="$(MSBuildThisFileDirectory)net8.0\Zakira.Imprint.Sdk.dll" />
<UsingTask TaskName="Zakira.Imprint.Sdk.ImprintCleanContent"
           AssemblyFile="$(MSBuildThisFileDirectory)net8.0\Zakira.Imprint.Sdk.dll" />
<UsingTask TaskName="Zakira.Imprint.Sdk.ImprintCleanMcpServers"
           AssemblyFile="$(MSBuildThisFileDirectory)net8.0\Zakira.Imprint.Sdk.dll" />
```

Using compiled tasks (rather than inline `RoslynCodeTaskFactory` tasks) was a deliberate choice — inline tasks cannot reference `System.Text.Json`, which is needed for JSON manipulation in the MCP merge logic.

---

## Agent Resolution

Agent resolution is the first step in every build-time operation. It determines which AI agents to target.

### The `AgentConfig` Class

`AgentConfig` (`src/Zakira.Imprint.Sdk/AgentConfig.cs`) is a static helper that:
- Defines known agents and their directory conventions
- Detects which agents are present on disk
- Resolves the final agent list based on configuration

### Known Agents

Three agents are defined out of the box:

| Agent | Detection Directory | Skills Sub-Path | MCP Sub-Path | MCP File |
|-------|-------------------|-----------------|--------------|----------|
| `copilot` | `.github` | `.github\skills` | `.vscode` | `mcp.json` |
| `claude` | `.claude` | `.claude\skills` | `.claude` | `mcp.json` |
| `cursor` | `.cursor` | `.cursor\rules` | `.cursor` | `mcp.json` |

Note that:
- Copilot uses `.github/` for detection and skills, but `.vscode/` for MCP — this matches VS Code's convention
- Claude uses `.claude/` for everything
- Cursor uses `.cursor/` for everything, but its skills directory is called `rules/` (matching Cursor's convention)

### Unknown Agents

If a consumer specifies an agent name that is not in the known list (e.g., `windsurf`), Imprint falls back to a convention:
- Skills: `.{name}/skills/`
- MCP: `.{name}/mcp.json`

This makes it possible to target new agents without waiting for an SDK update.

### Resolution Priority

The `ResolveAgents` method implements a four-tier fallback:

```
1. ImprintTargetAgents (explicit)    → "claude;cursor"     → [claude, cursor]
2. Auto-detect (scan directories)     → .github/ exists     → [copilot]
3. ImprintDefaultAgents (fallback)   → "copilot"           → [copilot]
4. Ultimate fallback                  →                     → [copilot]
```

- **Tier 1**: If `ImprintTargetAgents` is non-empty, parse it (semicolon or comma-separated, case-insensitive, deduplicated) and return immediately. No detection is performed.
- **Tier 2**: If `ImprintAutoDetectAgents` is true (default), scan the project directory for each known agent's detection directory. Return all detected agents.
- **Tier 3**: If no agents were detected, parse `ImprintDefaultAgents` and return that list.
- **Tier 4**: If everything else is empty, return `["copilot"]`.

### MSBuild Integration

All four MSBuild tasks receive agent configuration as parameters:

```xml
<ImprintCopyContent
    ContentItems="@(ImprintContent)"
    ProjectDirectory="$(MSBuildProjectDirectory)"
    TargetAgents="$(ImprintTargetAgents)"
    AutoDetectAgents="$(ImprintAutoDetectAgents)"
    DefaultAgents="$(ImprintDefaultAgents)" />
```

Each task calls `AgentConfig.ResolveAgents()` internally with these values.

---

## Build-Time Flow

The complete build-time flow:

```
dotnet build
    │
    ├─ NuGet Restore
    │   └─ Restores skill packages + Zakira.Imprint.Sdk
    │
    ├─ MSBuild Import
    │   ├─ Zakira.Imprint.Sdk.props (default properties)
    │   ├─ Zakira.Imprint.Sample.targets (declares ImprintContent items)
    │   ├─ Zakira.Imprint.AzureBestPractices.targets (declares ImprintContent items)
    │   └─ Zakira.Imprint.Sdk.targets (registers tasks, defines targets)
    │
    ├─ Imprint_CopyContent (BeforeTargets="BeforeBuild")
    │   ├─ Resolve agents: [copilot, claude]
    │   ├─ For each agent:
    │   │   ├─ Compute skills path (.github/skills/, .claude/skills/)
    │   │   └─ Copy all ImprintContent items to that path
    │   ├─ Write .imprint/manifest.json (unified v2)
    │   ├─ Write .imprint/{PackageId}.manifest (legacy, per-package)
    │   └─ Write .imprint/.gitignore
    │
    ├─ Imprint_MergeMcp (BeforeTargets="BeforeBuild")
    │   ├─ Parse all ImprintMcpFragment items
    │   ├─ Collect server definitions from fragments
    │   ├─ Resolve agents: [copilot, claude]
    │   ├─ For each agent:
    │   │   ├─ Compute MCP path (.vscode/mcp.json, .claude/mcp.json)
    │   │   ├─ Read existing mcp.json (if any)
    │   │   ├─ Remove old managed servers (from previous build)
    │   │   ├─ Add/update new managed servers
    │   │   ├─ Write mcp.json (only if content changed)
    │   │   └─ Write legacy .imprint-mcp-manifest
    │   └─ Update .imprint/manifest.json mcp section
    │
    ├─ [Normal build: compile, link, etc.]
    │
    └─ Build complete
```

### Design-Time Build Skipping

IDE background builds (design-time builds) are skipped to avoid performance issues. The targets check for `DesignTimeBuild`:

```xml
<Target Name="Imprint_CopyContent"
        BeforeTargets="BeforeBuild"
        Condition="'$(DesignTimeBuild)' != 'true' AND '@(ImprintContent)' != ''">
```

This means skills and MCP configs are only processed during explicit `dotnet build` or `dotnet publish` operations.

---

## Content Copy Pipeline

The `ImprintCopyContent` task (`ImprintCopyContent.cs`) handles skill file distribution.

### Input Processing

Each `ImprintContent` item has three metadata values:
- `PackageId` — Which package owns this file
- `SourceBase` — Root of the source directory (for computing relative paths)
- `DestinationBase` — Legacy property (no longer used for path computation; agents determine paths)

The task groups items by `PackageId`, then computes relative paths by stripping `SourceBase` from each item's full path.

### Per-Agent Copy

For each resolved agent:

1. Get the agent's skills directory via `AgentConfig.GetSkillsPath(projectDir, agent)`
2. For each package's files, copy to `{skillsDir}/{relativePath}`
3. Create directories as needed
4. Track all copied file paths per-agent per-package

### Example

Given a package with `skills/personal/SKILL.md` and agents `[copilot, claude]`:

```
Source:      C:\Users\...\.nuget\packages\zakira.imprint.sample\1.2.0\skills\personal\SKILL.md
SourceBase:  C:\Users\...\.nuget\packages\zakira.imprint.sample\1.2.0\skills\
Relative:    personal\SKILL.md

→ copilot:   P:\MyProject\.github\skills\personal\SKILL.md
→ claude:    P:\MyProject\.claude\skills\personal\SKILL.md
```

### Idempotent Writes

The copy task does not check timestamps — it always copies. This is intentional: the operation is fast (small files) and ensures consistency. The `mcp.json` merge, by contrast, does check for content changes before writing.

---

## MCP Server Merge Pipeline

The `ImprintMergeMcpServers` task (`ImprintMergeMcpServers.cs`) handles MCP configuration merging.

### Fragment Format

Each skill package can include a `mcp/<PackageId>.mcp.json` fragment:

```json
{
  "servers": {
    "my-server": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@my-org/my-mcp-server"]
    }
  }
}
```

Fragments only contain a `"servers"` object. Each key is a server name; the value is the server configuration.

### Merge Process

For each resolved agent:

1. Compute the MCP file path via `AgentConfig.GetMcpPath(projectDir, agent)`
2. Read the existing `mcp.json` (if it exists)
3. Read the legacy `.imprint-mcp-manifest` to get previously managed server keys
4. **Remove** all previously managed servers from `mcp.json`
5. **Add** all servers from the current set of fragments
6. Compare the result to the existing file content
7. **Write only if changed** (prevents git noise from timestamp-only changes)
8. Update the legacy manifest and the unified manifest

### User Servers Are Never Touched

The merge logic distinguishes between managed servers (tracked in the manifest) and user-defined servers (everything else). On clean, only managed servers are removed. If a user adds a server called `my-custom-server` to `mcp.json`, it survives all Imprint operations.

### Top-Level Properties Preserved

`mcp.json` can contain top-level properties beyond `"servers"`, such as `"inputs"` (used by VS Code for secret prompts). These are preserved through all merge and clean operations.

### Idempotent Write Check

Before writing `mcp.json`, the task serializes the new content and compares it to the existing file (byte-for-byte after normalization). If identical, no write occurs. This prevents unnecessary git diffs.

---

## Unified Manifest (v2)

Imprint v1.1.0 introduced a unified manifest at `.imprint/manifest.json` that tracks everything in one file.

### Format

```json
{
  "version": 2,
  "packages": {
    "Zakira.Imprint.Sample": {
      "files": {
        "copilot": [
          "P:\\MyProject\\.github\\skills\\personal\\SKILL.md"
        ],
        "claude": [
          "P:\\MyProject\\.claude\\skills\\personal\\SKILL.md"
        ]
      }
    },
    "Zakira.Imprint.AzureBestPractices": {
      "files": {
        "copilot": [
          "P:\\MyProject\\.github\\skills\\SKILL-AzureSecurity.md"
        ],
        "claude": [
          "P:\\MyProject\\.claude\\skills\\SKILL-AzureSecurity.md"
        ]
      }
    }
  },
  "mcp": {
    "copilot": {
      "path": ".vscode/mcp.json",
      "managedServers": ["sample-echo-server", "azure-mcp-server"]
    },
    "claude": {
      "path": ".claude/mcp.json",
      "managedServers": ["sample-echo-server", "azure-mcp-server"]
    }
  }
}
```

### Why a Unified Manifest?

The v1 approach used separate per-package manifest files (`.imprint/{PackageId}.manifest`). This worked for single-agent, but with multi-agent support:
- Clean needs to know all files across all agents
- MCP clean needs to know managed servers per-agent
- A single file is simpler to read and reason about

Legacy per-package manifests are still written for backward compatibility with older SDK versions.

### Manifest Location

The manifest lives at `.imprint/manifest.json` in the project directory. The `.imprint/` directory also contains a `.gitignore` (with `*`) to prevent any manifest files from being committed.

---

## Clean Pipeline

### Content Clean (`ImprintCleanContent`)

On `dotnet clean`:

1. Read `.imprint/manifest.json` (v2 format)
2. For each package, for each agent, delete every tracked file
3. Fall back to legacy `.imprint/*.manifest` files if the unified manifest is missing
4. Recursively remove empty directories (deepest first) — walks up from each deleted file's directory
5. Clean up the `.imprint/` directory itself if no manifests remain

The clean task does **not** re-resolve agents. It relies entirely on the manifest data. This means if you built with agents `[copilot, claude]` and then changed `ImprintTargetAgents` to `[cursor]`, clean will still remove the copilot and claude files correctly because they are tracked in the manifest.

### MCP Clean (`ImprintCleanMcpServers`)

On `dotnet clean`:

1. Read `.imprint/manifest.json` `"mcp"` section
2. For each agent in the manifest:
   - Read the agent's `mcp.json`
   - Remove all managed server keys
   - If no servers or other properties remain, delete `mcp.json`
   - Otherwise, rewrite `mcp.json` without the managed servers
3. Fall back to legacy `.imprint-mcp-manifest` files in `.vscode/`, `.claude/`, `.cursor/`
4. Remove the `"mcp"` section from the unified manifest
5. If the unified manifest has no `"packages"` data either, delete it

### Empty Directory Cleanup

After deleting files, the clean task walks up the directory tree and removes any empty directories. This prevents orphaned empty folders from accumulating. The walk stops at the project directory — it never deletes the project root.

---

## Gitignore Management

Imprint manages `.gitignore` files to prevent managed content from being committed:

### Skills Gitignore

A `.imprint/.gitignore` is created with content `*`, which prevents all manifest files from being tracked. The `.imprint/` directory itself is not gitignored — only its contents.

### MCP Manifest Gitignore

Each agent's MCP directory gets a `.imprint-mcp-manifest` file (legacy tracking). The MCP merge task ensures the agent directory's `.gitignore` includes `.imprint-mcp-manifest`.

### User Can Commit `mcp.json`

The `mcp.json` file itself is **not** gitignored. Users can commit it to source control if desired. Only the tracking manifests are gitignored.

---

## Two Package Patterns

### Skills-Only Package

```xml
<PropertyGroup>
  <TargetFramework>netstandard2.0</TargetFramework>
  <IncludeBuildOutput>false</IncludeBuildOutput>    <!-- No DLL in the package -->
  <DevelopmentDependency>true</DevelopmentDependency> <!-- Build-time only -->
  <NoPackageAnalysis>true</NoPackageAnalysis>
  <SuppressDependenciesWhenPacking>false</SuppressDependenciesWhenPacking>
</PropertyGroup>
```

Key points:
- `IncludeBuildOutput=false` — No DLL is included in the package
- `DevelopmentDependency=true` — NuGet marks this as build-time only; it does not flow to consumers' consumers
- `SuppressDependenciesWhenPacking=false` — Ensures the `Zakira.Imprint.Sdk` dependency is included in the `.nuspec`

### Code + Skills Package

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <!-- IncludeBuildOutput defaults to true — DLL ships in lib/ -->
  <!-- Do NOT set DevelopmentDependency — consumers need the runtime DLL -->
</PropertyGroup>

<PackageReference Include="Zakira.Imprint.Sdk" Version="1.1.0">
  <PrivateAssets>compile</PrivateAssets>
</PackageReference>
```

Key points:
- The DLL ships normally in `lib/net8.0/`
- `PrivateAssets="compile"` on the SDK reference means:
  - The SDK's build targets still flow to consumers (for skill/MCP operations)
  - The SDK is not a runtime dependency of the consumer
- The package is a real runtime dependency, not a development dependency

---

## Design Decisions

### Why Compiled MSBuild Tasks?

Early prototypes used `RoslynCodeTaskFactory` for inline C# tasks. This approach failed because:
- Inline tasks cannot reference NuGet packages like `System.Text.Json`
- JSON merging for MCP configs requires a proper JSON library
- Compiled tasks are faster (no per-build compilation)
- Compiled tasks support full .NET APIs

### Why `buildTransitive/`?

Without `buildTransitive/`, the SDK's targets would only be imported for projects that directly reference `Zakira.Imprint.Sdk`. Since consumers reference skill packages (not the SDK directly), the transitive folder is essential for the targets to reach the consumer.

### Why Agent-Native Paths?

An earlier design placed all skills in a single directory (e.g., `.imprint/skills/`) with symlinks or manifests pointing agents to the right location. This was rejected because:
- Each agent has different conventions and expectations
- Copilot expects `.github/skills/`, Claude expects `.claude/skills/`, Cursor expects `.cursor/rules/`
- Agents may not follow symlinks
- Placing files directly in native locations is the most reliable approach

### Why Not Use `DestinationBase` Metadata for Agent Paths?

The `DestinationBase` metadata on `ImprintContent` items is a legacy property from v1.0.0. In v1.1.0, the copy task computes destinations from `AgentConfig.GetSkillsPath()` instead. `DestinationBase` is still accepted (and declared in skill package targets) but is effectively ignored by the copy task.

### Why Write Both Unified and Legacy Manifests?

Backward compatibility. If a consumer has a mix of SDK versions (e.g., one skill package references SDK 1.0.0, another references 1.1.0), the legacy per-package manifests ensure clean operations work regardless of which SDK version runs the clean task.

### Why Does Clean Ignore Agent Resolution?

The clean task reads manifest data and deletes whatever is tracked, regardless of the current agent configuration. This is intentional:
- If you change `ImprintTargetAgents` between builds, old files from previously targeted agents are still cleaned up
- The manifest is the source of truth for what was installed

---

## Extending Imprint

### Adding a New Agent

To add support for a new agent (e.g., Windsurf):

1. Add an entry to `AgentConfig.KnownAgents` dictionary in `AgentConfig.cs`:
   ```csharp
   { "windsurf", new AgentDefinition(".windsurf", @".windsurf\skills", @".windsurf", "mcp.json") }
   ```

2. Rebuild and repack `Zakira.Imprint.Sdk`

No changes are needed in skill packages or their `.targets` files.

Alternatively, consumers can target unknown agents without any SDK changes by setting:
```xml
<ImprintTargetAgents>windsurf</ImprintTargetAgents>
```
This uses the fallback convention (`.windsurf/skills/` and `.windsurf/mcp.json`).

### Creating a New Skill Package

1. Use `dotnet new imprint -n MyOrg.Skills` or create manually
2. Add skill files to `skills/`
3. Optionally add MCP fragments to `mcp/`
4. Reference `Zakira.Imprint.Sdk 1.1.0` with `<PrivateAssets>compile</PrivateAssets>`
5. Create a `build/{PackageId}.targets` declaring `ImprintContent` and `ImprintMcpFragment` items
6. Pack: `dotnet pack -o ./packages`

### Custom Agent Paths

If an agent uses non-standard paths, consumers can target it as a known agent and override paths by contributing to the agent config, or use the explicit path properties for single-agent scenarios.
