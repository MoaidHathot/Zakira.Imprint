---
layout: default
title: Publishing Packages
parent: Guides
nav_order: 5
description: "Publish Imprint skill packages to NuGet.org or private feeds."
permalink: /guides/publishing-packages
---

# Publishing Packages
{: .fs-9 }

Publish your skill packages to NuGet.org or private feeds.
{: .fs-6 .fw-300 }

---

## Prerequisites

- A completed skill package (see [Creating Skill Packages](/guides/creating-skill-packages))
- A NuGet.org account (for public packages)
- An API key from NuGet.org

---

## Preparing for Publication

### 1. Update Package Metadata

Ensure your `.csproj` has complete metadata:

```xml
<PropertyGroup>
  <PackageId>MyOrg.Skills.Security</PackageId>
  <Version>1.0.0</Version>
  <Authors>Your Name</Authors>
  <Company>Your Organization</Company>
  <Description>Security best practices skills for AI assistants</Description>
  <PackageTags>ai;skills;security;copilot;claude;cursor;imprint</PackageTags>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageProjectUrl>https://github.com/your-org/skills-security</PackageProjectUrl>
  <RepositoryUrl>https://github.com/your-org/skills-security</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageIcon>icon.png</PackageIcon>
</PropertyGroup>
```

### 2. Add a README

Include a README in your package:

```xml
<ItemGroup>
  <None Include="README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

### 3. Add an Icon (Optional)

Include a package icon:

```xml
<ItemGroup>
  <None Include="icon.png" Pack="true" PackagePath="\" />
</ItemGroup>
```

---

## Building the Package

### Release Build

```bash
dotnet pack -c Release -o ./packages
```

### With Version Override

```bash
dotnet pack -c Release -o ./packages -p:Version=1.0.1
```

---

## Publishing to NuGet.org

### Get an API Key

1. Go to [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys)
2. Create a new API key
3. Select "Push" scope
4. Optionally restrict to specific packages

### Push the Package

```bash
dotnet nuget push packages/MyOrg.Skills.Security.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### Verify Publication

Your package should appear at:
```
https://www.nuget.org/packages/MyOrg.Skills.Security
```

{: .note }
> It may take a few minutes for the package to be indexed and searchable.

---

## Publishing to Private Feeds

### Azure Artifacts

```bash
# Add source (one-time)
dotnet nuget add source https://pkgs.dev.azure.com/ORG/_packaging/FEED/nuget/v3/index.json \
  --name "AzureArtifacts" \
  --username "PAT" \
  --password YOUR_PAT

# Push
dotnet nuget push packages/MyOrg.Skills.Security.1.0.0.nupkg \
  --source "AzureArtifacts" \
  --api-key az
```

### GitHub Packages

```bash
# Add source (one-time)
dotnet nuget add source https://nuget.pkg.github.com/OWNER/index.json \
  --name "GitHub" \
  --username YOUR_USERNAME \
  --password YOUR_GITHUB_TOKEN

# Push
dotnet nuget push packages/MyOrg.Skills.Security.1.0.0.nupkg \
  --source "GitHub"
```

### Local Feed

For testing or internal distribution:

```bash
# Create local feed directory
mkdir -p ~/nuget-packages

# Push to local feed
dotnet nuget push packages/MyOrg.Skills.Security.1.0.0.nupkg \
  --source ~/nuget-packages
```

---

## Versioning Strategy

### Semantic Versioning

Follow [SemVer](https://semver.org/):

- **MAJOR** (1.0.0 → 2.0.0): Breaking changes to skill content
- **MINOR** (1.0.0 → 1.1.0): New skills added
- **PATCH** (1.0.0 → 1.0.1): Typo fixes, clarifications

### Pre-release Versions

```bash
dotnet pack -c Release -p:Version=1.1.0-preview.1
```

---

## CI/CD Integration

### GitHub Actions

```yaml
name: Publish Package

on:
  release:
    types: [published]

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Pack
        run: dotnet pack -c Release -o ./packages -p:Version=${{ github.event.release.tag_name }}
      
      - name: Push to NuGet
        run: |
          dotnet nuget push packages/*.nupkg \
            --api-key ${{ secrets.NUGET_API_KEY }} \
            --source https://api.nuget.org/v3/index.json
```

### Azure Pipelines

```yaml
trigger:
  tags:
    include:
      - 'v*'

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  inputs:
    version: '8.0.x'

- script: |
    VERSION=$(echo $BUILD_SOURCEBRANCH | sed 's/refs\/tags\/v//')
    dotnet pack -c Release -o ./packages -p:Version=$VERSION
  displayName: 'Pack'

- task: NuGetCommand@2
  inputs:
    command: 'push'
    packagesToPush: 'packages/*.nupkg'
    nuGetFeedType: 'external'
    publishFeedCredentials: 'NuGet.org'
```

---

## Package Validation

### Before Publishing

1. **Test locally** - Install in a test project and build
2. **Verify contents** - Inspect the `.nupkg` file
3. **Check dependencies** - Ensure Imprint SDK is included

### Inspect Package Contents

```bash
# Using unzip
unzip -l packages/MyOrg.Skills.Security.1.0.0.nupkg

# Or using NuGet Package Explorer (GUI tool)
```

### Expected Structure

```
MyOrg.Skills.Security.1.0.0.nupkg
├── build/
│   └── MyOrg.Skills.Security.targets
├── skills/
│   ├── authentication/
│   │   └── SKILL.md
│   └── encryption/
│       └── SKILL.md
├── mcp/
│   └── MyOrg.Skills.Security.mcp.json
├── README.md
├── icon.png
└── MyOrg.Skills.Security.nuspec
```

---

## Updating Published Packages

NuGet doesn't allow overwriting published versions. To fix issues:

1. **Delete** the package version (if within first hour and no downloads)
2. **Deprecate** the version (recommended)
3. **Publish** a new patch version

### Deprecating a Version

```bash
# Via nuget.org UI or API
dotnet nuget deprecate MyOrg.Skills.Security \
  --version 1.0.0 \
  --reason "Use 1.0.1 instead - fixed typos" \
  --api-key YOUR_API_KEY
```

---

## Best Practices

### README Content

Include in your README:
- What skills are included
- How to install
- Required environment variables (for MCP)
- Changelog

### Tags

Use relevant tags for discoverability:
- `ai`
- `skills`
- `copilot`
- `claude`
- `cursor`
- `imprint`
- Domain-specific tags (`security`, `azure`, etc.)

### License

Always specify a license:
- `MIT` - Most permissive
- `Apache-2.0` - Patent protection
- `Proprietary` - For commercial packages

---

## Next Steps

- [Configuration Reference](/reference/configuration) - All configuration options
- [Troubleshooting](/troubleshooting) - Common issues
