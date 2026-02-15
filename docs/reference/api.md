---
layout: default
title: API Reference
parent: Reference
nav_order: 3
---

# API Reference

Complete reference for MSBuild tasks, item groups, and their parameters.

---

## MSBuild Tasks

All tasks are defined in `Zakira.Imprint.Sdk.dll` and registered via `<UsingTask>` in the SDK's .targets file.

---

### ImprintGenerateTargets

Generates a `.targets` file for a skill package at pack time.

**Executes:** Before `_GetPackageFiles` (pack time only)

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `ImprintItems` | `ITaskItem[]` | Yes | - | The `<Imprint>` items to process |
| `PackageId` | `string` | Yes | - | The NuGet package ID |
| `OutputPath` | `string` | Yes | - | Directory for generated .targets file |
| `EnabledByDefault` | `bool` | No | `true` | Whether skills are enabled by default |

#### Output

| Parameter | Type | Description |
|-----------|------|-------------|
| `GeneratedTargetsFile` | `string` | Path to the generated .targets file |

#### Example Usage

```xml
<ImprintGenerateTargets
    ImprintItems="@(Imprint)"
    PackageId="$(PackageId)"
    OutputPath="$(IntermediateOutputPath)Imprint\"
    EnabledByDefault="$(ImprintEnabledByDefault)">
  <Output TaskParameter="GeneratedTargetsFile" PropertyName="_GeneratedFile" />
</ImprintGenerateTargets>
```

---

### ImprintCopyContent

Copies skill files from NuGet packages to agent-specific directories.

**Executes:** Before `BeforeBuild`

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `ContentItems` | `ITaskItem[]` | Yes | - | `<ImprintContent>` items to copy |
| `ProjectDirectory` | `string` | Yes | - | Consumer project directory |
| `TargetAgents` | `string` | No | *(empty)* | Explicit agents (semicolon-separated) |
| `AutoDetectAgents` | `bool` | No | `true` | Auto-detect agents from directories |
| `DefaultAgents` | `string` | No | `copilot` | Fallback agents |
| `PrefixSkills` | `bool` | No | `false` | Add package prefix to folders |
| `DefaultPrefix` | `string` | No | *(empty)* | Custom prefix (uses PackageId if empty) |

#### ImprintContent Item Metadata

| Metadata | Required | Description |
|----------|----------|-------------|
| `DestinationBase` | Yes | Root destination directory |
| `PackageId` | Yes | Source package identifier |
| `SourceBase` | Yes | Root source directory |
| `SuggestedPrefix` | No | Author's suggested prefix |
| `ImprintPrefix` | No | Consumer override prefix |
| `ImprintUsePrefix` | No | Override global prefix setting |
| `ImprintEnabled` | No | Enable/disable this content |
| `EnabledByDefault` | No | Package author's default setting |

#### Example Usage

```xml
<ImprintCopyContent
    ContentItems="@(ImprintContent)"
    ProjectDirectory="$(MSBuildProjectDirectory)"
    TargetAgents="$(ImprintTargetAgents)"
    AutoDetectAgents="$(ImprintAutoDetectAgents)"
    DefaultAgents="$(ImprintDefaultAgents)"
    PrefixSkills="$(ImprintPrefixSkills)"
    DefaultPrefix="$(ImprintDefaultPrefix)" />
```

---

### ImprintCleanContent

Removes skill files previously copied by `ImprintCopyContent`.

**Executes:** After `Clean`

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `ProjectDirectory` | `string` | Yes | - | Consumer project directory |
| `TargetAgents` | `string` | No | *(empty)* | Accepted for compatibility |
| `AutoDetectAgents` | `bool` | No | `true` | Accepted for compatibility |
| `DefaultAgents` | `string` | No | `copilot` | Accepted for compatibility |

{: .note }
The clean task uses the manifest to determine which files to remove, so agent parameters are not used for actual cleanup logic.

#### Example Usage

```xml
<ImprintCleanContent
    ProjectDirectory="$(MSBuildProjectDirectory)"
    TargetAgents="$(ImprintTargetAgents)"
    AutoDetectAgents="$(ImprintAutoDetectAgents)"
    DefaultAgents="$(ImprintDefaultAgents)" />
```

---

### ImprintMergeMcpServers

Merges MCP server fragment files into agent-specific `mcp.json` files.

**Executes:** Before `BeforeBuild`

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `McpFragmentFiles` | `ITaskItem[]` | Yes | - | `<ImprintMcpFragment>` items |
| `ProjectDirectory` | `string` | Yes | - | Consumer project directory |
| `TargetAgents` | `string` | No | *(empty)* | Explicit agents |
| `AutoDetectAgents` | `bool` | No | `true` | Auto-detect agents |
| `DefaultAgents` | `string` | No | `copilot` | Fallback agents |

#### ImprintMcpFragment Item Metadata

| Metadata | Required | Description |
|----------|----------|-------------|
| `PackageId` | Yes | Source package identifier |
| `ImprintEnabled` | No | Enable/disable this fragment |
| `EnabledByDefault` | No | Package author's default setting |

#### MCP Fragment File Format

```json
{
  "servers": {
    "server-name": {
      "type": "stdio",
      "command": "node",
      "args": ["path/to/server.js"],
      "env": {
        "NODE_ENV": "production"
      }
    }
  }
}
```

#### Example Usage

```xml
<ImprintMergeMcpServers
    McpFragmentFiles="@(ImprintMcpFragment)"
    ProjectDirectory="$(MSBuildProjectDirectory)"
    TargetAgents="$(ImprintTargetAgents)"
    AutoDetectAgents="$(ImprintAutoDetectAgents)"
    DefaultAgents="$(ImprintDefaultAgents)" />
```

---

### ImprintCleanMcpServers

Removes managed MCP servers from agent `mcp.json` files.

**Executes:** After `Clean`

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `ProjectDirectory` | `string` | Yes | - | Consumer project directory |
| `TargetAgents` | `string` | No | *(empty)* | Explicit agents |
| `AutoDetectAgents` | `bool` | No | `true` | Auto-detect agents |
| `DefaultAgents` | `string` | No | `copilot` | Fallback agents |

#### Example Usage

```xml
<ImprintCleanMcpServers
    ProjectDirectory="$(MSBuildProjectDirectory)"
    TargetAgents="$(ImprintTargetAgents)"
    AutoDetectAgents="$(ImprintAutoDetectAgents)"
    DefaultAgents="$(ImprintDefaultAgents)" />
```

---

## Item Groups

### Imprint

Declares skill and MCP files in a package author's project.

**Scope:** Package author projects

```xml
<ItemGroup>
  <!-- Skill files (default type) -->
  <Imprint Include="skills\**\*" />
  
  <!-- Explicit skill type -->
  <Imprint Include="prompts\**\*.md" Type="Skill" />
  
  <!-- MCP server fragments -->
  <Imprint Include="mcp\*.mcp.json" Type="Mcp" />
  
  <!-- With suggested prefix -->
  <Imprint Include="skills\**\*" SuggestedPrefix="myco" />
</ItemGroup>
```

#### Metadata

| Metadata | Default | Description |
|----------|---------|-------------|
| `Type` | `Skill` | Content type: `Skill` or `Mcp` |
| `SuggestedPrefix` | *(empty)* | Author's suggested prefix for consumers |
| `DestinationBase` | `$(ImprintSkillsPath)` | Override destination directory |

---

### ImprintContent

Generated by package .targets files; consumed by `ImprintCopyContent`.

**Scope:** Consumer projects (auto-generated)

```xml
<ItemGroup>
  <ImprintContent Include="$(PkgMyPackage)\content\skills\**\*">
    <DestinationBase>$(ImprintSkillsPath)</DestinationBase>
    <PackageId>MyPackage</PackageId>
    <SourceBase>$(PkgMyPackage)\content\skills\</SourceBase>
  </ImprintContent>
</ItemGroup>
```

#### Metadata

| Metadata | Required | Description |
|----------|----------|-------------|
| `DestinationBase` | Yes | Root destination directory |
| `PackageId` | Yes | Source package identifier |
| `SourceBase` | Yes | Root source directory for relative path calculation |
| `SuggestedPrefix` | No | Package author's suggested prefix |
| `EnabledByDefault` | No | Whether enabled by default (from package) |
| `ImprintPrefix` | No | Consumer-specified prefix |
| `ImprintUsePrefix` | No | Consumer override for prefix behavior |
| `ImprintEnabled` | No | Consumer enable/disable override |

---

### ImprintMcpFragment

Generated by package .targets files; consumed by `ImprintMergeMcpServers`.

**Scope:** Consumer projects (auto-generated)

```xml
<ItemGroup>
  <ImprintMcpFragment Include="$(PkgMyPackage)\content\mcp\servers.mcp.json">
    <PackageId>MyPackage</PackageId>
  </ImprintMcpFragment>
</ItemGroup>
```

#### Metadata

| Metadata | Required | Description |
|----------|----------|-------------|
| `PackageId` | Yes | Source package identifier |
| `EnabledByDefault` | No | Whether enabled by default |
| `ImprintEnabled` | No | Consumer enable/disable override |

---

## MSBuild Targets

### Pack-Time Targets

| Target | Runs Before | Purpose |
|--------|-------------|---------|
| `Imprint_GenerateTargetsFile` | `_GetPackageFiles` | Generate .targets from `<Imprint>` items |
| `Imprint_IncludeContentInPackage` | `_GetPackageFiles` | Add files to NuGet package |

### Build-Time Targets

| Target | Runs Before/After | Purpose |
|--------|-------------------|---------|
| `Imprint_ApplyPackageReferenceMetadata` | Before `Imprint_CopyContent` | Apply consumer overrides |
| `Imprint_CopyContent` | Before `BeforeBuild` | Copy skill files |
| `Imprint_ApplyMcpPackageReferenceMetadata` | Before `Imprint_MergeMcp` | Apply MCP overrides |
| `Imprint_MergeMcp` | Before `BeforeBuild` | Merge MCP servers |

### Clean-Time Targets

| Target | Runs After | Purpose |
|--------|------------|---------|
| `Imprint_CleanContent` | `Clean` | Remove copied skill files |
| `Imprint_CleanMcp` | `Clean` | Remove managed MCP servers |

---

## Static API (AgentConfig)

The `AgentConfig` class provides static methods for agent resolution.

**Namespace:** `Zakira.Imprint.Sdk`

### Methods

#### ResolveAgents

```csharp
public static List<string> ResolveAgents(
    string projectDirectory,
    string targetAgents,
    bool autoDetect,
    string defaultAgents)
```

Resolves the final list of target agents using priority:
1. Explicit `targetAgents` parameter
2. Auto-detection (if `autoDetect` is true)
3. `defaultAgents` fallback

#### DetectAgents

```csharp
public static List<string> DetectAgents(string projectDirectory)
```

Scans for agent directories (`.github`, `.claude`, `.cursor`) and returns detected agents.

#### GetSkillsPath

```csharp
public static string GetSkillsPath(string projectDirectory, string agentName)
```

Returns the absolute skills destination path for an agent.

#### GetMcpPath

```csharp
public static string GetMcpPath(string projectDirectory, string agentName)
```

Returns the absolute MCP config file path for an agent.

#### GetMcpRootKey

```csharp
public static string GetMcpRootKey(string agentName)
```

Returns the JSON root key for MCP servers (`"servers"` for VS Code, `"mcpServers"` for Claude/Cursor).

### KnownAgents Dictionary

```csharp
public static readonly IReadOnlyDictionary<string, AgentDefinition> KnownAgents
```

Contains definitions for known agents with their directory conventions.

### AgentDefinition Record

```csharp
public record AgentDefinition(
    string Name,           // "copilot", "claude", "cursor"
    string DetectionDir,   // ".github", ".claude", ".cursor"
    string SkillsSubPath,  // ".github/skills", ".claude/skills", ".cursor/rules"
    string McpSubPath,     // ".vscode", ".claude", ".cursor"
    string McpFileName,    // "mcp.json"
    string McpRootKey      // "servers" or "mcpServers"
);
```
