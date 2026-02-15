---
layout: default
title: Getting Started
nav_order: 2
description: "Get started with Zakira.Imprint - create and consume AI skill packages in minutes."
permalink: /getting-started
---

# Getting Started
{: .fs-9 }

Create and consume AI skill packages in minutes.
{: .fs-6 .fw-300 }

---

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- An AI assistant (GitHub Copilot, Claude, or Cursor)

---

## Quick Start: Consuming Skill Packages

The fastest way to get started is consuming an existing skill package.

### Step 1: Install a Skill Package

```bash
# Add a skill package to your project
dotnet add package Zakira.Imprint.Sample
```

### Step 2: Build Your Project

```bash
dotnet build
```

### Step 3: Verify Installation

After building, check that the skill files have been copied:

```bash
# For GitHub Copilot users
ls .github/skills/

# For Claude users
ls .claude/skills/

# For Cursor users
ls .cursor/rules/
```

That's it! Your AI assistant now has access to the skills from the package.

---

## Quick Start: Creating Skill Packages

Ready to create your own skill package? Here's the fastest path.

### Step 1: Create a New Project

```bash
mkdir MyOrg.Skills
cd MyOrg.Skills
dotnet new classlib -n MyOrg.Skills
cd MyOrg.Skills
```

### Step 2: Configure the Project

Replace the contents of `MyOrg.Skills.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    
    <!-- Package metadata -->
    <PackageId>MyOrg.Skills</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>AI Skills for our organization</Description>
    
    <!-- Skills-only package settings -->
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
    <NoPackageAnalysis>true</NoPackageAnalysis>
    <SuppressDependenciesWhenPacking>false</SuppressDependenciesWhenPacking>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Zakira.Imprint.Sdk" Version="1.1.0">
      <PrivateAssets>compile</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <!-- Include all files in skills folder -->
    <Imprint Include="skills\**\*" />
  </ItemGroup>
</Project>
```

### Step 3: Create Your Skills

Create the skills directory and add your first skill:

```bash
mkdir skills
mkdir skills\coding-standards
```

Create `skills/coding-standards/SKILL.md`:

```markdown
# Coding Standards

## Naming Conventions

- Use PascalCase for public members
- Use camelCase for private fields (with underscore prefix: _fieldName)
- Use meaningful, descriptive names
- Avoid abbreviations except for well-known ones (Id, Url, etc.)

## Code Organization

- One class per file
- Keep methods under 30 lines
- Maximum 3 levels of nesting

## Error Handling

- Use specific exception types
- Always include meaningful error messages
- Log exceptions with full context
```

### Step 4: Build and Pack

```bash
dotnet build
dotnet pack -o ./packages
```

### Step 5: Test Locally

In a test project, add a reference to your local package:

```bash
# In a different project directory
dotnet add package MyOrg.Skills --source ../MyOrg.Skills/packages
dotnet build
```

Check that your skills appear in the agent directories!

---

## Understanding the Build Output

When you build a project with Imprint packages installed, several things happen:

### Files Created

| Location | Purpose |
|:---------|:--------|
| `.github/skills/*` | Skills for GitHub Copilot |
| `.claude/skills/*` | Skills for Claude |
| `.cursor/rules/*` | Skills for Cursor |
| `.imprint/manifest.json` | Tracks installed files for cleanup |
| `.imprint/.gitignore` | Prevents manifest from being committed |

### What Happens on Build

```
dotnet build
    │
    ├─ NuGet Restore (restores skill packages)
    │
    ├─ Imprint_CopyContent (copies skills to agent directories)
    │
    ├─ Imprint_MergeMcp (merges MCP server configs)
    │
    └─ Normal build continues...
```

### What Happens on Clean

```
dotnet clean
    │
    ├─ Imprint_CleanContent (removes tracked skill files)
    │
    ├─ Imprint_CleanMcpServers (removes managed MCP servers)
    │
    └─ Normal clean continues...
```

---

## Agent Auto-Detection

Imprint automatically detects which AI agents you're using by looking for their configuration directories:

| Directory Found | Agent Detected |
|:----------------|:---------------|
| `.github/` | GitHub Copilot |
| `.claude/` | Claude |
| `.cursor/` | Cursor |

If no agent directories are found, Imprint defaults to targeting GitHub Copilot.

### Overriding Auto-Detection

You can explicitly specify which agents to target:

```xml
<PropertyGroup>
  <!-- Target specific agents -->
  <ImprintTargetAgents>copilot;claude</ImprintTargetAgents>
</PropertyGroup>
```

---

## Next Steps

Now that you have the basics:

1. **Learn the concepts**: Read [How Imprint Works](/concepts/overview) to understand the architecture
2. **Create skill packages**: Follow the [Creating Skill Packages](/guides/creating-skill-packages) guide
3. **Add MCP servers**: Learn about [MCP Integration](/concepts/mcp-integration)
4. **Configure everything**: See the [Configuration Reference](/reference/configuration)

---

## Common Issues

### Skills Not Appearing

1. **Check the build output** - Look for `Imprint_CopyContent` in the build logs
2. **Verify agent directories exist** - Create `.github/`, `.claude/`, or `.cursor/` if needed
3. **Check the manifest** - Look at `.imprint/manifest.json` to see what was installed

### Package Not Working

1. **Verify SDK reference** - The skill package must reference `Zakira.Imprint.Sdk`
2. **Check item declarations** - Ensure `<Imprint Include="...">` items are correctly defined
3. **Rebuild** - Run `dotnet clean` then `dotnet build`

See the [Troubleshooting](/troubleshooting) page for more solutions.
