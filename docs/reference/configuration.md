---
layout: default
title: Configuration
parent: Reference
nav_order: 1
---

# Configuration Reference

Complete reference for all MSBuild properties used to configure Zakira.Imprint SDK behavior.

---

## Agent Detection Properties

These properties control which AI agents receive skill files.

### ImprintAutoDetectAgents

Enables automatic detection of AI agents based on existing directories in the project.

| | |
|---|---|
| **Type** | Boolean |
| **Default** | `true` |
| **Scope** | Consumer projects |

When enabled, Imprint scans the project directory for existing agent folders:
- `.github/` → Copilot detected
- `.claude/` → Claude detected  
- `.cursor/` → Cursor detected

```xml
<PropertyGroup>
  <ImprintAutoDetectAgents>true</ImprintAutoDetectAgents>
</PropertyGroup>
```

---

### ImprintTargetAgents

Explicitly specifies which agents to target. When set, disables auto-detection.

| | |
|---|---|
| **Type** | String (semicolon-separated) |
| **Default** | *(empty)* |
| **Scope** | Consumer projects |
| **Values** | `copilot`, `claude`, `cursor` |

```xml
<PropertyGroup>
  <!-- Target only Copilot and Claude -->
  <ImprintTargetAgents>copilot;claude</ImprintTargetAgents>
</PropertyGroup>
```

---

### ImprintDefaultAgents

Fallback agents when auto-detection finds no existing agent directories.

| | |
|---|---|
| **Type** | String (semicolon-separated) |
| **Default** | `copilot` |
| **Scope** | Consumer projects |

```xml
<PropertyGroup>
  <!-- Default to all agents if none detected -->
  <ImprintDefaultAgents>copilot;claude;cursor</ImprintDefaultAgents>
</PropertyGroup>
```

---

## Path Properties

These properties control where files are copied.

### ImprintSkillsPath

Legacy property for custom skills destination (single-agent mode fallback).

| | |
|---|---|
| **Type** | Path |
| **Default** | `$(MSBuildProjectDirectory)\.github\skills\` |
| **Scope** | Consumer projects |

{: .note }
In multi-agent mode, paths are determined automatically per agent. This property only applies when using explicit single-agent configuration.

```xml
<PropertyGroup>
  <ImprintSkillsPath>$(MSBuildProjectDirectory)\custom-skills\</ImprintSkillsPath>
</PropertyGroup>
```

---

### ImprintPromptsPath

Destination directory for prompt files.

| | |
|---|---|
| **Type** | Path |
| **Default** | `$(MSBuildProjectDirectory)\.github\prompts\` |
| **Scope** | Consumer projects |

```xml
<PropertyGroup>
  <ImprintPromptsPath>$(MSBuildProjectDirectory)\.prompts\</ImprintPromptsPath>
</PropertyGroup>
```

---

### ImprintMcpPath

Destination directory for MCP configuration files.

| | |
|---|---|
| **Type** | Path |
| **Default** | `$(MSBuildProjectDirectory)\.vscode\` |
| **Scope** | Consumer projects |

```xml
<PropertyGroup>
  <ImprintMcpPath>$(MSBuildProjectDirectory)\.config\</ImprintMcpPath>
</PropertyGroup>
```

---

## Skill Prefixing Properties

These properties control how skill folders are named to avoid conflicts.

### ImprintPrefixSkills

Global setting to enable/disable package name prefixing for skill folders.

| | |
|---|---|
| **Type** | Boolean |
| **Default** | `false` |
| **Scope** | Consumer projects |

When `true`, skills from `MyCompany.Skills` package are placed in `MyCompany.Skills/` subfolder instead of directly in the skills directory.

```xml
<PropertyGroup>
  <ImprintPrefixSkills>true</ImprintPrefixSkills>
</PropertyGroup>
```

**Result:**
```
.github/skills/
├── MyCompany.Skills/
│   └── coding/
│       └── SKILL.md
└── AnotherPackage/
    └── debugging/
        └── SKILL.md
```

---

### ImprintDefaultPrefix

Custom prefix to use instead of package ID when prefixing is enabled.

| | |
|---|---|
| **Type** | String |
| **Default** | *(empty - uses PackageId)* |
| **Scope** | Consumer projects |

```xml
<PropertyGroup>
  <ImprintPrefixSkills>true</ImprintPrefixSkills>
  <ImprintDefaultPrefix>vendor</ImprintDefaultPrefix>
</PropertyGroup>
```

---

## Package Author Properties

These properties are used when creating skill packages.

### ImprintEnabledByDefault

Controls whether skills from this package are enabled by default when consumed.

| | |
|---|---|
| **Type** | Boolean |
| **Default** | `true` |
| **Scope** | Skill package authors |

Set to `false` for optional or experimental skills that consumers must explicitly enable.

```xml
<PropertyGroup>
  <ImprintEnabledByDefault>false</ImprintEnabledByDefault>
</PropertyGroup>
```

Consumers can override per-package:
```xml
<PackageReference Include="MyCompany.OptionalSkills" Version="1.0.0">
  <ImprintEnabled>true</ImprintEnabled>
</PackageReference>
```

---

## PackageReference Metadata

Consumers can configure individual packages using metadata on `PackageReference` items.

### ImprintEnabled

Enable or disable skills from a specific package.

| | |
|---|---|
| **Type** | Boolean |
| **Default** | Package's `EnabledByDefault` setting |
| **Scope** | Consumer PackageReference |

```xml
<PackageReference Include="MyCompany.Skills" Version="1.0.0">
  <ImprintEnabled>false</ImprintEnabled>
</PackageReference>
```

---

### ImprintUsePrefix

Override global `ImprintPrefixSkills` for a specific package.

| | |
|---|---|
| **Type** | Boolean |
| **Default** | Global `ImprintPrefixSkills` value |
| **Scope** | Consumer PackageReference |

```xml
<PackageReference Include="MyCompany.Skills" Version="1.0.0">
  <ImprintUsePrefix>true</ImprintUsePrefix>
</PackageReference>
```

---

### ImprintPrefix

Custom prefix for a specific package's skills.

| | |
|---|---|
| **Type** | String |
| **Default** | Package ID |
| **Scope** | Consumer PackageReference |

```xml
<PackageReference Include="MyCompany.VeryLongPackageName.Skills" Version="1.0.0">
  <ImprintUsePrefix>true</ImprintUsePrefix>
  <ImprintPrefix>myco</ImprintPrefix>
</PackageReference>
```

**Result:** Skills placed in `.github/skills/myco/` instead of the full package name.

---

## Diagnostic Options

### MSBuild Verbose Logging

Use MSBuild's built-in verbosity options to see detailed Imprint output:

```bash
# Windows
dotnet build -v detailed

# Or with binary log for analysis
dotnet build -bl
```

Imprint tasks output messages at different importance levels:
- **High**: Key operations (files copied, MCP servers merged)
- **Normal**: Detailed progress information
- **Low**: Diagnostic details

---

## Complete Example

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    
    <!-- Target specific agents -->
    <ImprintTargetAgents>copilot;claude</ImprintTargetAgents>
    
    <!-- Enable skill prefixing globally -->
    <ImprintPrefixSkills>true</ImprintPrefixSkills>
  </PropertyGroup>

  <ItemGroup>
    <!-- Standard package with prefixing -->
    <PackageReference Include="Contoso.CodingSkills" Version="2.0.0" />
    
    <!-- Package with custom short prefix -->
    <PackageReference Include="MyCompany.EnterpriseSkills" Version="1.0.0">
      <ImprintPrefix>enterprise</ImprintPrefix>
    </PackageReference>
    
    <!-- Disable skills from this package -->
    <PackageReference Include="Experimental.Skills" Version="0.1.0">
      <ImprintEnabled>false</ImprintEnabled>
    </PackageReference>
    
    <!-- Package without prefixing (override global) -->
    <PackageReference Include="Core.Skills" Version="1.0.0">
      <ImprintUsePrefix>false</ImprintUsePrefix>
    </PackageReference>
  </ItemGroup>
</Project>
```

---

## Property Precedence

When multiple configuration sources exist, precedence is:

1. **PackageReference metadata** (highest priority)
2. **Project-level PropertyGroup**
3. **SDK defaults** (lowest priority)

For `ImprintEnabled` specifically:
1. Consumer's `PackageReference.ImprintEnabled`
2. Package author's `ImprintEnabledByDefault`
3. SDK default (`true`)
