# Zakira.Imprint.Sdk

Core MSBuild task engine for building Imprint AI Skills packages. Provides compiled MSBuild tasks that handle:

- **Auto-generated `.targets` files** - Declare `<Imprint>` items in your `.csproj` and the SDK generates the `.targets` file at pack time
- **Multi-agent support** - Targets Copilot, Claude, and Cursor simultaneously, placing files in each agent's native directory
- **Content copying** - Copies skill files from NuGet packages to the consumer's project at build time, preserving folder structure
- **MCP server merging** - Merges MCP (Model Context Protocol) server configurations from multiple packages into each agent's `mcp.json`
- **Clean support** - Removes all managed files and MCP servers on `dotnet clean`
- **Unified manifest tracking** - A single `.imprint/manifest.json` tracks all files and MCP servers per-agent per-package

## For Skill Package Authors

Reference this SDK in your skill package `.csproj` and declare your content using `<Imprint>` items:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <PackageId>MyOrg.Skills</PackageId>
    <Version>1.0.0</Version>
    
    <!-- Skills-only package (no compiled code) -->
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Zakira.Imprint.Sdk" Version="1.0.0" />
  </ItemGroup>

  <!-- Declare your content -->
  <ItemGroup>
    <Imprint Include="skills\**\*" />                           <!-- Skills (default type) -->
    <Imprint Include="mcp\*.mcp.json" Type="Mcp" />             <!-- MCP server configs -->
  </ItemGroup>
</Project>
```

That's it! The SDK auto-generates the `.targets` file at pack time and handles agent resolution, file copying, MCP merging, manifest tracking, and cleanup.

## Multi-Agent Support

The SDK automatically detects which AI agents are present and distributes content to each one:

| Agent | Detection | Skills Path | MCP Path | MCP Root Key |
|-------|-----------|-------------|----------|--------------|
| `copilot` | `.github/` exists | `.github/skills/` | `.vscode/mcp.json` | `servers` |
| `claude` | `.claude/` exists | `.claude/skills/` | `.claude/mcp.json` | `mcpServers` |
| `cursor` | `.cursor/` exists | `.cursor/rules/` | `.cursor/mcp.json` | `mcpServers` |

### MCP Schema Transformation

Different AI agents use different JSON root keys for MCP configuration. Package authors always write fragments using `"servers"` — the SDK automatically transforms to each agent's expected schema when writing to their `mcp.json` files.

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
