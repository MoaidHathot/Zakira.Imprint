---
layout: default
title: Consuming Packages
parent: Guides
nav_order: 4
description: "How to install and use Imprint skill packages."
permalink: /guides/consuming-packages
---

# Consuming Packages
{: .fs-9 }

Install and use AI skill packages in your projects.
{: .fs-6 .fw-300 }

---

## Overview

Consuming Imprint packages is as simple as installing any NuGet package. On build, skills are automatically deployed to your AI assistants.

---

## Installing Packages

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

## Building Your Project

After installing, simply build:

```bash
dotnet build
```

Imprint automatically:
1. Detects which AI agents you use
2. Copies skills to each agent's directory
3. Merges MCP servers into `mcp.json`
4. Updates the tracking manifest

---

## Verifying Installation

### Check Skill Files

```bash
# GitHub Copilot
ls .github/skills/

# Claude
ls .claude/skills/

# Cursor
ls .cursor/rules/

# Roo Code
ls .roo/rules/
```

### Check MCP Configuration

```bash
# VS Code / Copilot
cat .vscode/mcp.json

# Claude
cat .claude/mcp.json

# Cursor
cat .cursor/mcp.json

# Roo Code
cat .roo/mcp.json
```

### Check the Manifest

```bash
cat .imprint/manifest.json
```

---

## Multiple Packages

You can install multiple skill packages:

```bash
dotnet add package MyOrg.Skills.Security
dotnet add package MyOrg.Skills.Coding
dotnet add package MyOrg.Skills.Azure
```

All skills are merged into the appropriate directories:

```
.github/skills/
├── security/           # From MyOrg.Skills.Security
│   └── SKILL.md
├── coding/             # From MyOrg.Skills.Coding
│   └── SKILL.md
└── azure/              # From MyOrg.Skills.Azure
    ├── storage/
    └── functions/
```

---

## Controlling Agent Targets

### Auto-Detection (Default)

By default, Imprint detects agents by looking for their directories:

| Directory | Agent |
|:----------|:------|
| `.github/` | copilot |
| `.claude/` | claude |
| `.cursor/` | cursor |
| `.roo/` | roo |

To enable auto-detection, create the agent directory:

```bash
mkdir .github   # Enable Copilot
mkdir .claude   # Enable Claude
mkdir .cursor   # Enable Cursor
mkdir .roo      # Enable Roo Code
```

### Explicit Configuration

Override auto-detection in your `.csproj`:

```xml
<PropertyGroup>
  <!-- Target specific agents only -->
  <ImprintTargetAgents>copilot;claude</ImprintTargetAgents>
</PropertyGroup>
```

### Disable for CI/CD

In CI environments, you typically don't need skills:

```xml
<PropertyGroup Condition="'$(CI)' == 'true'">
  <ImprintTargetAgents></ImprintTargetAgents>
  <ImprintAutoDetectAgents>false</ImprintAutoDetectAgents>
</PropertyGroup>
```

---

## Updating Packages

Update to the latest version:

```bash
dotnet add package PackageName --version X.Y.Z
```

Or update all packages:

```bash
dotnet outdated
dotnet add package PackageName
```

On the next build:
1. Old skills are removed
2. New skills are installed
3. MCP servers are updated

---

## Removing Packages

Remove the package:

```bash
dotnet remove package PackageName
```

Clean up installed files:

```bash
dotnet clean
```

Or just rebuild - the clean happens automatically.

---

## Understanding the Build Output

During build, you'll see Imprint messages:

```
Imprint: Resolved agents: copilot, claude
Imprint: Copying skills from MyOrg.Skills.Security
Imprint: Copied 3 files to .github/skills/
Imprint: Copied 3 files to .claude/skills/
Imprint: Merging MCP servers
Imprint: Added 2 servers to .vscode/mcp.json
Imprint: Added 2 servers to .claude/mcp.json
```

---

## Gitignore Considerations

### Default Behavior

By default, skill files and `mcp.json` are NOT gitignored. This allows teams to:
- Share a consistent set of AI skills
- Commit MCP configurations

### Ignoring Generated Files

If you prefer to not commit generated files:

```gitignore
# Ignore Imprint manifest
.imprint/

# Ignore generated skills
.github/skills/
.claude/skills/
.cursor/rules/
.roo/rules/

# Ignore generated MCP configs (if desired)
# .vscode/mcp.json
# .claude/mcp.json
# .cursor/mcp.json
# .roo/mcp.json
```

---

## Working with MCP Servers

### Servers Are Merged

Imprint merges MCP servers from packages into your existing `mcp.json`:

**Your existing mcp.json:**
```json
{
  "servers": {
    "my-custom-server": { ... }
  }
}
```

**After installing a package:**
```json
{
  "servers": {
    "my-custom-server": { ... },
    "package-server": { ... }
  }
}
```

### Your Servers Are Safe

Imprint only manages servers that came from packages. Your custom servers are never touched.

### Environment Variables

Some MCP servers require environment variables:

```bash
# Set required environment variables
export MY_API_KEY="your-key-here"
export DATABASE_URL="connection-string"
```

Check the package documentation for required variables.

---

## Troubleshooting

### Skills Not Appearing

**Check agent directories exist:**
```bash
ls -la | grep -E '^\.'
# Should show .github, .claude, .cursor, or .roo
```

**Check the manifest:**
```bash
cat .imprint/manifest.json
```

**Rebuild:**
```bash
dotnet clean
dotnet build
```

### MCP Server Not Connecting

**Verify server is defined:**
```bash
cat .vscode/mcp.json
```

**Test server manually:**
```bash
npx -y @org/server-name
```

**Check environment variables:**
```bash
echo $MY_API_KEY
```

### Wrong Agents Targeted

**Check current configuration:**
```bash
grep -i imprint *.csproj
```

**Check auto-detection directories:**
```bash
ls -d .* 2>/dev/null | grep -E '\.github|\.claude|\.cursor'
```

---

## Advanced: Private Package Feeds

### Azure Artifacts

```xml
<!-- nuget.config -->
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="MyOrg" value="https://pkgs.dev.azure.com/myorg/_packaging/feed/nuget/v3/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <MyOrg>
      <add key="Username" value="PAT" />
      <add key="ClearTextPassword" value="%AZURE_DEVOPS_PAT%" />
    </MyOrg>
  </packageSourceCredentials>
</configuration>
```

### GitHub Packages

```xml
<!-- nuget.config -->
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github" value="https://nuget.pkg.github.com/OWNER/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="USERNAME" />
      <add key="ClearTextPassword" value="%GITHUB_TOKEN%" />
    </github>
  </packageSourceCredentials>
</configuration>
```

---

## Next Steps

- [Configuration Reference]({{ site.baseurl }}/reference/configuration) - All configuration options
- [Troubleshooting]({{ site.baseurl }}/troubleshooting) - Common issues and solutions
