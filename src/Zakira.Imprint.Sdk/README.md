# Zakira.Imprint.Sdk

Core MSBuild task engine for building Imprint AI Skills packages. Provides compiled MSBuild tasks that handle:

- **Multi-agent support** - Targets Copilot, Claude, and Cursor simultaneously, placing files in each agent's native directory
- **Content copying** - Copies skill files from NuGet packages to the consumer's project at build time, preserving folder structure
- **MCP server merging** - Merges MCP (Model Context Protocol) server configurations from multiple packages into each agent's `mcp.json`
- **Clean support** - Removes all managed files and MCP servers on `dotnet clean`
- **Unified manifest tracking** - A single `.imprint/manifest.json` tracks all files and MCP servers per-agent per-package

## For Skill Package Authors

Reference this SDK in your skill package `.csproj`:

```xml
<PackageReference Include="Zakira.Imprint.Sdk" Version="1.1.0">
  <PrivateAssets>compile</PrivateAssets>
</PackageReference>
```

Then create a simple `.targets` file in your `build/` folder:

```xml
<Project>
  <PropertyGroup>
    <_MyPkg_Root>$(MSBuildThisFileDirectory)..\</_MyPkg_Root>
  </PropertyGroup>
  <ItemGroup>
    <ImprintContent Include="$(_MyPkg_Root)skills\**\*">
      <DestinationBase>$(ImprintSkillsPath)</DestinationBase>
      <PackageId>MyPackage</PackageId>
      <SourceBase>$(_MyPkg_Root)skills\</SourceBase>
    </ImprintContent>
    <ImprintMcpFragment Include="$(_MyPkg_Root)mcp\*.mcp.json" />
  </ItemGroup>
</Project>
```

That is it. The SDK handles agent resolution, file copying to each agent's native directory, MCP merging, manifest tracking, and cleanup.

## Multi-Agent Support

The SDK automatically detects which AI agents are present and distributes content to each one:

| Agent | Detection | Skills Path | MCP Path |
|-------|-----------|-------------|----------|
| `copilot` | `.github/` exists | `.github/skills/` | `.vscode/mcp.json` |
| `claude` | `.claude/` exists | `.claude/skills/` | `.claude/mcp.json` |
| `cursor` | `.cursor/` exists | `.cursor/rules/` | `.cursor/mcp.json` |

### Resolution Priority

1. **Explicit** — `ImprintTargetAgents` property (semicolon-separated)
2. **Auto-detect** — Scan for agent directories (default behavior)
3. **Default** — `ImprintDefaultAgents` property (default: `copilot`)

## For Consumers

Install any Imprint package and build. Skills are copied automatically to all detected agents.

### Configuration Properties

| Property | Default | Purpose |
|----------|---------|---------|
| `ImprintTargetAgents` | *(empty)* | Explicit agent list. Overrides auto-detection. |
| `ImprintAutoDetectAgents` | `true` | Scan for agent directories at build time |
| `ImprintDefaultAgents` | `copilot` | Fallback when no agents are detected |

### Examples

```xml
<!-- Target only Claude and Cursor -->
<PropertyGroup>
  <ImprintTargetAgents>claude;cursor</ImprintTargetAgents>
</PropertyGroup>
```

```xml
<!-- Disable auto-detection, always target Copilot -->
<PropertyGroup>
  <ImprintAutoDetectAgents>false</ImprintAutoDetectAgents>
  <ImprintDefaultAgents>copilot</ImprintDefaultAgents>
</PropertyGroup>
```

### Legacy Path Overrides

These properties are still available for backward compatibility:

| Property | Default |
|----------|---------|
| `ImprintSkillsPath` | `.github/skills/` |
| `ImprintPromptsPath` | `.github/prompts/` |
| `ImprintMcpPath` | `.vscode/` |

## MSBuild Tasks

| Task | Purpose |
|------|---------|
| `ImprintCopyContent` | Copy skill files to each agent's skills directory |
| `ImprintCleanContent` | Remove tracked skill files on clean |
| `ImprintMergeMcpServers` | Merge MCP fragments into each agent's `mcp.json` |
| `ImprintCleanMcpServers` | Remove managed MCP servers on clean |

## MSBuild Targets

| Target | Trigger | Description |
|--------|---------|-------------|
| `Imprint_CopyContent` | `BeforeTargets="BeforeBuild"` | Copies skills (skips design-time builds) |
| `Imprint_MergeMcp` | `BeforeTargets="BeforeBuild"` | Merges MCP configs (skips design-time builds) |
| `Imprint_CleanContent` | `AfterTargets="Clean"` | Removes tracked skills |
| `Imprint_CleanMcp` | `AfterTargets="Clean"` | Removes managed MCP servers |
