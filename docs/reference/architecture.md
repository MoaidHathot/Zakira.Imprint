---
layout: default
title: Architecture
parent: Reference
nav_order: 2
---

# Architecture Deep-Dive

This document provides a comprehensive look at how Zakira.Imprint SDK works internally, from package creation to skill deployment.

---

## System Overview

Zakira.Imprint operates in two distinct phases:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PACK TIME (Author)                                │
│                                                                             │
│  ┌─────────────┐    ┌──────────────────────┐    ┌───────────────────────┐   │
│  │ <Imprint>   │───▶│ ImprintGenerateTargets│───▶│ {PackageId}.targets   │   │
│  │ items       │    │ MSBuild Task         │    │ (auto-generated)      │   │
│  └─────────────┘    └──────────────────────┘    └───────────────────────┘   │
│                                                          │                  │
│                                                          ▼                  │
│                                                 ┌───────────────────────┐   │
│                                                 │ NuGet Package         │   │
│                                                 │ ├── build/            │   │
│                                                 │ │   └── *.targets     │   │
│                                                 │ ├── buildTransitive/  │   │
│                                                 │ │   └── *.targets     │   │
│                                                 │ └── content/          │   │
│                                                 │     └── skills/       │   │
│                                                 └───────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         BUILD TIME (Consumer)                               │
│                                                                             │
│  ┌─────────────┐    ┌──────────────────────┐    ┌───────────────────────┐   │
│  │ Package     │───▶│ ImprintCopyContent   │───▶│ .github/skills/       │   │
│  │ .targets    │    │ MSBuild Task         │    │ .claude/skills/       │   │
│  │ (ImprintCon-│    └──────────────────────┘    │ .cursor/rules/        │   │
│  │ tent items) │                                └───────────────────────┘   │
│  └─────────────┘                                                            │
│                                                                             │
│  ┌─────────────┐    ┌──────────────────────┐    ┌───────────────────────┐   │
│  │ Package     │───▶│ ImprintMergeMcpServers│───▶│ .vscode/mcp.json      │   │
│  │ MCP frags   │    │ MSBuild Task         │    │ .claude/mcp.json      │   │
│  │ (ImprintMcp-│    └──────────────────────┘    │ .cursor/mcp.json      │   │
│  │ Fragment)   │                                └───────────────────────┘   │
│  └─────────────┘                                                            │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Pack-Time Processing

### 1. Target Generation (`ImprintGenerateTargets`)

When you run `dotnet pack` on a project with `<Imprint>` items, the `Imprint_GenerateTargetsFile` target executes:

**Source:** `src/Zakira.Imprint.Sdk/ImprintGenerateTargets.cs`

**Process:**
1. Reads all `<Imprint>` items from the project
2. Groups items by type (Skill vs MCP)
3. Generates a `.targets` file containing `<ImprintContent>` and `<ImprintMcpFragment>` items
4. Writes to `obj/Imprint/{PackageId}.targets`

**Generated .targets structure:**
```xml
<Project>
  <PropertyGroup>
    <_Imprint_MyPackage_Root>$(MSBuildThisFileDirectory)..\content\</_Imprint_MyPackage_Root>
  </PropertyGroup>

  <ItemGroup>
    <ImprintContent Include="$(_Imprint_MyPackage_Root)skills\**\*">
      <DestinationBase>$(ImprintSkillsPath)</DestinationBase>
      <PackageId>MyPackage</PackageId>
      <SourceBase>$(_Imprint_MyPackage_Root)skills\</SourceBase>
    </ImprintContent>
  </ItemGroup>
</Project>
```

### 2. Package Content Inclusion

The `Imprint_IncludeContentInPackage` target adds files to the NuGet package:

- **Skills/prompts:** Packed to `content/{relative-path}`
- **Generated .targets:** Packed to both `build/` and `buildTransitive/`

The `buildTransitive` folder ensures the .targets file is applied to projects that transitively reference the package.

---

## Build-Time Processing

### 1. Agent Resolution (`AgentConfig`)

Before copying files, Imprint determines which AI agents to target.

**Source:** `src/Zakira.Imprint.Sdk/AgentConfig.cs`

**Resolution order:**
1. **Explicit setting:** `<ImprintTargetAgents>copilot;claude</ImprintTargetAgents>`
2. **Auto-detection:** Scans for `.github/`, `.claude/`, `.cursor/` directories
3. **Fallback:** Uses `<ImprintDefaultAgents>` (default: `copilot`)

**Known agents:**

| Agent | Detection Dir | Skills Path | MCP Config | MCP Root Key |
|-------|---------------|-------------|------------|--------------|
| copilot | `.github` | `.github/skills/` | `.vscode/mcp.json` | `servers` |
| claude | `.claude` | `.claude/skills/` | `.claude/mcp.json` | `mcpServers` |
| cursor | `.cursor` | `.cursor/rules/` | `.cursor/mcp.json` | `mcpServers` |

### 2. Skill File Copying (`ImprintCopyContent`)

**Source:** `src/Zakira.Imprint.Sdk/ImprintCopyContent.cs`

**Process:**
1. Resolves target agents
2. Parses `<ImprintContent>` items into per-package groups
3. Checks for destination conflicts (fails build if two packages target same path)
4. Cleans up files from previously-installed but now-removed packages
5. Copies files to each agent's skills directory
6. Updates `.gitignore` files in skill directories
7. Writes unified manifest to `.imprint/manifest.json`

**Conflict detection:**
```
Package A: skills/coding/SKILL.md → .github/skills/coding/SKILL.md
Package B: skills/coding/SKILL.md → .github/skills/coding/SKILL.md
                                    ↑ CONFLICT!
```

Solution: Use `<ImprintPrefix>` on one of the packages.

### 3. MCP Server Merging (`ImprintMergeMcpServers`)

**Source:** `src/Zakira.Imprint.Sdk/ImprintMergeMcpServers.cs`

**Process:**
1. Reads all `<ImprintMcpFragment>` items (JSON files with server definitions)
2. Collects servers from all fragments
3. For each target agent:
   - Reads existing `mcp.json` (preserves user content)
   - Removes previously-managed servers no longer in fragments
   - Adds/updates servers from current fragments
   - Writes updated `mcp.json`

**MCP fragment format:**
```json
{
  "servers": {
    "my-mcp-server": {
      "type": "stdio",
      "command": "node",
      "args": ["path/to/server.js"]
    }
  }
}
```

**Agent-specific root keys:**
- VS Code/Copilot: `"servers"` (matches VS Code MCP extension)
- Claude/Cursor: `"mcpServers"` (matches their native format)

---

## Manifest System

Imprint uses a manifest system to track deployed files for incremental updates and cleanup.

### Unified Manifest (v2)

**Location:** `.imprint/manifest.json`

```json
{
  "version": 2,
  "packages": {
    "MyCompany.Skills": {
      "files": {
        "copilot": [
          "P:\\Project\\.github\\skills\\coding\\SKILL.md"
        ],
        "claude": [
          "P:\\Project\\.claude\\skills\\coding\\SKILL.md"
        ]
      }
    }
  },
  "mcp": {
    "copilot": {
      "path": ".vscode\\mcp.json",
      "managedServers": ["my-mcp-server"]
    }
  }
}
```

### Legacy Manifests

For backward compatibility, per-package manifests are also written:

- **Skills:** `.imprint/{PackageId}.manifest`
- **MCP:** `.vscode/.imprint-mcp-manifest`, `.claude/.imprint-mcp-manifest`, etc.

---

## Cleanup Operations

### Build-Time Cleanup

`ImprintCopyContent` performs incremental cleanup:
1. Reads the unified manifest
2. Identifies packages no longer in `<PackageReference>`
3. Deletes their files
4. Removes their `.gitignore` entries
5. Deletes empty directories

### Clean Target

The `ImprintCleanContent` task runs during `dotnet clean`:

**Source:** `src/Zakira.Imprint.Sdk/ImprintCleanContent.cs`

1. Reads unified manifest (falls back to legacy manifests)
2. Deletes all tracked files
3. Cleans `.gitignore` entries
4. Removes empty directories
5. Deletes the `.imprint/` directory if empty

---

## .gitignore Management

Imprint creates and manages `.gitignore` files in skill directories to prevent auto-generated files from being committed.

**Format:**
```gitignore
# Managed by Zakira.Imprint (MyCompany.Skills)
SKILL.md
helper.md
# Managed by Zakira.Imprint (AnotherPackage)
other-skill.md
```

**Behavior:**
- Preserves user-added entries
- Groups entries by source package
- Removes sections when packages are uninstalled
- Deletes the file if it becomes empty

---

## MSBuild Target Execution Order

```
Build
  │
  ├─▶ Imprint_ApplyPackageReferenceMetadata
  │     (merges consumer overrides from PackageReference)
  │
  ├─▶ Imprint_CopyContent  ◀── BeforeTargets="BeforeBuild"
  │     (copies skill files to agent directories)
  │
  ├─▶ Imprint_ApplyMcpPackageReferenceMetadata
  │     (handles MCP EnabledByDefault overrides)
  │
  └─▶ Imprint_MergeMcp  ◀── BeforeTargets="BeforeBuild"
        (merges MCP server fragments)

Pack
  │
  ├─▶ Imprint_GenerateTargetsFile  ◀── BeforeTargets="_GetPackageFiles"
  │     (generates .targets from <Imprint> items)
  │
  └─▶ Imprint_IncludeContentInPackage  ◀── BeforeTargets="_GetPackageFiles"
        (adds content files and .targets to package)

Clean
  │
  ├─▶ Imprint_CleanContent  ◀── AfterTargets="Clean"
  │     (removes copied skill files)
  │
  └─▶ Imprint_CleanMcp  ◀── AfterTargets="Clean"
        (removes managed MCP servers)
```

---

## Prefix System

The prefix system prevents naming conflicts when multiple packages provide similarly-named skills.

### Without Prefixing (Default)
```
Package A: skills/coding/SKILL.md
Package B: skills/coding/SKILL.md
           → CONFLICT (build fails)
```

### With Global Prefixing
```xml
<ImprintPrefixSkills>true</ImprintPrefixSkills>
```

```
Package A: skills/coding/SKILL.md → PackageA/coding/SKILL.md
Package B: skills/coding/SKILL.md → PackageB/coding/SKILL.md
           → OK (no conflict)
```

### With Custom Prefix
```xml
<PackageReference Include="VeryLongPackageName.Skills">
  <ImprintUsePrefix>true</ImprintUsePrefix>
  <ImprintPrefix>short</ImprintPrefix>
</PackageReference>
```

```
skills/coding/SKILL.md → short/coding/SKILL.md
```

### Prefix Priority

1. `PackageReference.ImprintPrefix` (explicit per-package)
2. `ImprintDefaultPrefix` (global custom prefix)
3. `ImprintContent.SuggestedPrefix` (author suggestion)
4. `PackageId` (fallback)

---

## Error Handling

### Destination Conflicts

Detected during `ImprintCopyContent.CheckForDestinationConflicts()`:

```
error : Zakira.Imprint.Sdk: Destination conflict - both 'PackageA' and 
'PackageB' are trying to copy to '.github\skills\coding\SKILL.md'. 
Consider using ImprintPrefix on one of the PackageReferences to avoid 
this conflict.
```

### Missing Files

Logged as warnings, build continues:

```
warning : Zakira.Imprint.Sdk: Source file not found: path\to\file.md
```

### Invalid MCP Fragments

Logged as warnings, fragment skipped:

```
warning : Zakira.Imprint.Sdk: Failed to parse fragment path\to\fragment.json: 
Invalid JSON
```

---

## Design Decisions

### Why Generate .targets at Pack Time?

- **Simplicity:** Consumers don't need to understand MSBuild item syntax
- **Flexibility:** Package authors define their content structure
- **Consistency:** All content paths are relative to package root

### Why Use MSBuild Tasks (Not Scripts)?

- **Cross-platform:** Works on Windows, Linux, macOS
- **Integration:** Native MSBuild execution with proper logging
- **Performance:** Compiled C# code, no script interpreter overhead

### Why Multi-Agent Support?

- **Ecosystem diversity:** Different teams use different AI tools
- **Future-proofing:** New agents can be added without package updates
- **Simplicity for authors:** Write once, deploy to all agents

### Why Manifest-Based Tracking?

- **Incremental updates:** Only copy changed files
- **Clean uninstall:** Remove all files when package is removed
- **Conflict detection:** Know which package owns which file
