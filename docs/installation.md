---
layout: default
title: Installation
nav_order: 3
description: "Install Zakira.Imprint SDK and skill packages."
permalink: /installation
---

# Installation
{: .fs-9 }

How to install Zakira.Imprint SDK and skill packages.
{: .fs-6 .fw-300 }

---

## Installing Skill Packages

To consume AI skills in your project, install a skill package via NuGet:

### Using .NET CLI

```bash
dotnet add package PackageName
```

### Using Package Manager Console

```powershell
Install-Package PackageName
```

### Using PackageReference

Add directly to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="PackageName" Version="1.0.0" />
</ItemGroup>
```

---

## Installing the SDK (Package Authors)

If you're creating skill packages, you need to reference the Imprint SDK:

### Via .NET CLI

```bash
dotnet add package Zakira.Imprint.Sdk
```

### Via PackageReference

```xml
<ItemGroup>
  <PackageReference Include="Zakira.Imprint.Sdk" Version="1.0.0-preview">
    <PrivateAssets>compile</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

{: .important }
> The `<PrivateAssets>compile</PrivateAssets>` setting is crucial. It ensures the SDK's build targets flow to consumers while keeping the SDK as a build-time-only dependency.

---

## Version Compatibility

| SDK Version | .NET SDK Required | Features |
|:------------|:------------------|:---------|
| 1.0.0-preview | .NET 8.0+ | Multi-agent, auto-generated targets, unified manifest |

---

## NuGet Sources

### Public Packages

Imprint packages are available on NuGet.org:

```bash
# SDK
dotnet add package Zakira.Imprint.Sdk

# Sample packages
dotnet add package Zakira.Imprint.Sample
```

### Private Feeds

For organizational packages, configure your private NuGet feed:

```xml
<!-- nuget.config -->
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="MyOrg" value="https://pkgs.dev.azure.com/myorg/_packaging/feed/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

### Local Development

For testing packages locally:

```xml
<!-- nuget.config -->
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="local" value="./local-packages" />
  </packageSources>
</configuration>
```

---

## Verifying Installation

### For Consumers

After installing a skill package and building:

1. Check that skill files exist in agent directories:
   ```bash
   ls .github/skills/   # Copilot
   ls .claude/skills/   # Claude
   ls .cursor/rules/    # Cursor
   ```

2. Check the manifest:
   ```bash
   cat .imprint/manifest.json
   ```

### For Package Authors

After building your skill package project:

1. Check the generated `.targets` file in the build output
2. Pack the project: `dotnet pack`
3. Inspect the package contents:
   ```bash
   # List package contents
   dotnet nuget locals global-packages -l
   # Navigate to package location and inspect
   ```

---

## Uninstalling

### Removing a Skill Package

```bash
dotnet remove package PackageName
dotnet build  # or dotnet clean
```

{: .note }
> Running `dotnet clean` or `dotnet build` after removing a package will clean up any skill files that were installed by that package.

### Complete Cleanup

To remove all Imprint-managed files:

```bash
dotnet clean
```

This removes:
- All skill files tracked in the manifest
- Managed MCP server entries from `mcp.json`
- The `.imprint/` directory

---

## Offline Installation

For air-gapped environments, download packages and their dependencies:

```bash
# Download packages to a local folder
dotnet restore --packages ./offline-packages

# Copy offline-packages to target machine
# Configure nuget.config to use the local folder
```

---

## CI/CD Integration

In CI/CD pipelines, skills are typically not needed. You can skip Imprint operations:

```bash
# Skills won't be installed during CI builds
dotnet build -p:ImprintAutoDetectAgents=false -p:ImprintTargetAgents=""
```

Or configure in your `.csproj`:

```xml
<PropertyGroup Condition="'$(CI)' == 'true'">
  <ImprintAutoDetectAgents>false</ImprintAutoDetectAgents>
  <ImprintTargetAgents></ImprintTargetAgents>
</PropertyGroup>
```

---

## Next Steps

- [Getting Started](/getting-started) - Quick start guide
- [Creating Skill Packages](/guides/creating-skill-packages) - Build your own packages
- [Configuration Reference](/reference/configuration) - All configuration options
