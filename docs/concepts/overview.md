---
layout: default
title: Overview
parent: Concepts
nav_order: 1
description: "How Zakira.Imprint works - the complete picture."
permalink: /concepts/overview
---

# How Imprint Works
{: .fs-9 }

Understanding the complete picture of Imprint's architecture.
{: .fs-6 .fw-300 }

---

## The Problem

AI assistants like GitHub Copilot, Claude, and Cursor can be enhanced with context-specific instructions called "skills" or "rules". These are typically Markdown files placed in special directories:

- GitHub Copilot: `.github/skills/`
- Claude: `.claude/skills/`
- Cursor: `.cursor/rules/`

The challenge is: **how do you distribute these skills across an organization or to users of your library?**

Manual copying doesn't scale. What if you have 50 projects? What if the skills get updated? What if you want to ship skills alongside a library?

---

## The Solution

Imprint solves this by leveraging the existing NuGet package distribution system:

```
┌─────────────────────────────────────────────────────────────────┐
│                       Package Author                             │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  MyOrg.Skills.Security                                    │   │
│  │  ├── skills/                                              │   │
│  │  │   ├── authentication/SKILL.md                          │   │
│  │  │   └── input-validation/SKILL.md                        │   │
│  │  ├── mcp/MyOrg.Skills.Security.mcp.json                   │   │
│  │  └── MyOrg.Skills.Security.csproj                         │   │
│  └──────────────────────────────────────────────────────────┘   │
│                            │                                     │
│                            ▼ dotnet pack                         │
│                    ┌───────────────┐                             │
│                    │   NuGet.org   │                             │
│                    └───────────────┘                             │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                         Consumer                                 │
│                            │                                     │
│                            ▼ dotnet add package                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  ConsumerProject                                          │   │
│  │  └── ConsumerProject.csproj (references the package)      │   │
│  └──────────────────────────────────────────────────────────┘   │
│                            │                                     │
│                            ▼ dotnet build                        │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  After Build:                                             │   │
│  │  ├── .github/skills/authentication/SKILL.md      (Copilot)│   │
│  │  ├── .github/skills/input-validation/SKILL.md    (Copilot)│   │
│  │  ├── .claude/skills/authentication/SKILL.md      (Claude) │   │
│  │  ├── .claude/skills/input-validation/SKILL.md    (Claude) │   │
│  │  ├── .vscode/mcp.json  ← servers merged          (Copilot)│   │
│  │  ├── .claude/mcp.json  ← servers merged          (Claude) │   │
│  │  └── .imprint/manifest.json                      (tracking)│   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Package Layering

Imprint uses a three-layer architecture:

### Layer 1: Zakira.Imprint.Sdk (The Engine)

The SDK is the core engine. It contains:

- **Compiled MSBuild tasks** that perform all file operations
- **Build targets** that hook into the MSBuild pipeline
- **Agent configuration** that knows where each AI assistant expects its files

Consumers never reference the SDK directly - it's a transitive dependency.

### Layer 2: Skill Packages (Content + Metadata)

Skill packages contain:

- **Skill files** (`.md`, `.txt`, any file type)
- **MCP fragments** (optional MCP server configurations)
- **A reference to the SDK** (for build-time operations)

Creating a skill package is simple - declare `<Imprint>` items in your `.csproj`:

```xml
<ItemGroup>
  <Imprint Include="skills\**\*" />
  <Imprint Include="mcp\*.mcp.json" Type="Mcp" />
</ItemGroup>
```

### Layer 3: Consumer Projects (Your Code)

Consumer projects simply reference skill packages:

```xml
<PackageReference Include="MyOrg.Skills.Security" Version="1.0.0" />
```

On build, the SDK's targets (flowing through the skill package) handle everything.

---

## Build-Time Flow

When you run `dotnet build` on a project with skill packages installed:

```
dotnet build
    │
    ├─ 1. NuGet Restore
    │      Restores skill packages and Zakira.Imprint.Sdk
    │
    ├─ 2. MSBuild Import
    │      ├─ Zakira.Imprint.Sdk.props (default properties)
    │      ├─ SkillPackage.targets (declares Imprint items)
    │      └─ Zakira.Imprint.Sdk.targets (task definitions)
    │
    ├─ 3. Imprint_CopyContent (BeforeTargets="BeforeBuild")
    │      ├─ Resolve target agents (auto-detect or explicit)
    │      ├─ For each agent: copy skills to native directory
    │      └─ Update .imprint/manifest.json
    │
    ├─ 4. Imprint_MergeMcp (BeforeTargets="BeforeBuild")
    │      ├─ Parse MCP fragments from all packages
    │      ├─ For each agent: merge into agent's mcp.json
    │      └─ Update manifest with managed servers
    │
    └─ 5. Normal Build Continues
           Compile, link, etc.
```

### Key Design Points

1. **BeforeTargets="BeforeBuild"** - Skills are installed early, so they're available during the build
2. **Design-time builds are skipped** - IDE background builds don't trigger file operations
3. **Idempotent operations** - Running build multiple times produces the same result

---

## Clean-Time Flow

When you run `dotnet clean`:

```
dotnet clean
    │
    ├─ 1. Imprint_CleanContent
    │      ├─ Read .imprint/manifest.json
    │      ├─ Delete all tracked skill files
    │      └─ Remove empty directories
    │
    ├─ 2. Imprint_CleanMcpServers
    │      ├─ Read manifest's MCP section
    │      ├─ Remove managed servers from each mcp.json
    │      └─ Delete mcp.json if empty
    │
    └─ 3. Normal Clean Continues
```

### Important: Clean Uses the Manifest

The clean operation doesn't re-resolve agents or re-read packages. It uses the manifest as the source of truth. This means:

- If you change `ImprintTargetAgents` between builds, old files are still cleaned
- If you remove a package reference, its files are cleaned on the next clean/build

---

## File Types Supported

Imprint is not limited to Markdown files. You can include:

- `.md` - Markdown skill files
- `.txt` - Plain text instructions
- `.json` - JSON configuration
- `.yaml` - YAML configuration
- Any other file type

All files in the `skills/` directory (or whatever you configure) are copied to agent directories.

---

## Gitignore Management

Imprint creates `.gitignore` files to prevent managed files from polluting your repository:

| Location | Contents | Purpose |
|:---------|:---------|:--------|
| `.imprint/.gitignore` | `*` | Prevents manifest files from being tracked |
| `.vscode/.gitignore` | `.imprint-mcp-manifest` | Prevents legacy MCP manifest tracking |

{: .note }
> The skill files and `mcp.json` are **not** gitignored by default. This is intentional - you may want to commit them for team consistency.

---

## Next Steps

- [Multi-Agent Support](/concepts/agents) - How Imprint targets multiple AI assistants
- [MCP Integration](/concepts/mcp-integration) - Distributing MCP server configurations
- [Package Patterns](/concepts/package-patterns) - Skills-only vs code+skills
