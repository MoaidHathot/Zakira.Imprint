---
layout: default
title: Home
nav_order: 1
description: "Zakira.Imprint - Distribute AI Skills via NuGet packages for GitHub Copilot, Claude, Cursor, and other AI assistants."
permalink: /
---

<p align="center">
  <img src="{{ '/assets/images/Zakira.Imprint.icon.png' | relative_url }}" alt="Zakira.Imprint Logo" width="128" height="128" />
</p>

# Zakira.Imprint
{: .fs-9 }

Distribute AI Skills via NuGet packages. Ship SKILL.md files for GitHub Copilot, Claude, Cursor, and other AI assistants as easily as shipping a library.
{: .fs-6 .fw-300 }

[Get Started](/getting-started){: .btn .btn-primary .fs-5 .mb-4 .mb-md-0 .mr-2 }
[View on GitHub](https://github.com/MoaidHathot/Zakira.Imprint){: .btn .fs-5 .mb-4 .mb-md-0 }

---

## What is Imprint?

**Imprint** enables distributing AI Skills (like `SKILLS.md` files) via NuGet packages. Think of it like how Roslyn Analyzers are distributed - you install a package, build your project, and the AI skills are automatically deployed to your AI assistants.

### How It Works

1. **Package authors** create NuGet packages containing skill files and optional MCP server configurations
2. **Developers** install these packages via `dotnet add package`
3. **On build**, skills are automatically copied to each AI agent's native directory
4. **AI assistants** immediately have access to the new capabilities

```
dotnet add package Contoso.Skills.AzureSecurity
dotnet build
# Skills are now available in GitHub Copilot, Claude, and Cursor!
```

## Key Features

### Multi-Agent Support

Imprint automatically detects and targets multiple AI assistants simultaneously:

| Agent | Skills Directory | MCP Config |
|:------|:-----------------|:-----------|
| GitHub Copilot | `.github/skills/` | `.vscode/mcp.json` |
| Claude | `.claude/skills/` | `.claude/mcp.json` |
| Cursor | `.cursor/rules/` | `.cursor/mcp.json` |

### Zero Configuration

Auto-detection means you don't need to configure anything. Imprint scans your project for agent directories and targets all detected agents automatically.

### MCP Server Distribution

Packages can include [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server configurations that are automatically merged into your AI assistant's config:

```json
{
  "servers": {
    "azure-mcp-server": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@azure/mcp-server"]
    }
  }
}
```

### Two Package Patterns

**Skills-Only Packages**
{: .text-green-200 }

Distribute AI guidance without any runtime code. Perfect for:
- Organization coding standards
- Security best practices
- Framework guidelines

**Code + Skills Packages**
{: .text-blue-200 }

Ship a library AND AI skills together. When developers install your library, they get both the code and the AI guidance on how to use it.

---

## Quick Example

### Creating a Skill Package

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Zakira.Imprint.Sdk" Version="1.1.0">
      <PrivateAssets>compile</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Imprint Include="skills\**\*" />
  </ItemGroup>
</Project>
```

### The Skill File

```markdown
<!-- skills/security/SKILL.md -->
# Security Best Practices

When writing authentication code:
- Always use parameterized queries
- Never store passwords in plain text
- Use HTTPS for all API calls
- Validate all user input
```

### Consuming the Package

```bash
dotnet add package MyOrg.Skills.Security
dotnet build
```

After build, the skill is available at:
- `.github/skills/security/SKILL.md` (Copilot)
- `.claude/skills/security/SKILL.md` (Claude)
- `.cursor/rules/security/SKILL.md` (Cursor)

---

## Use Cases

{: .highlight }
> **Organization Standards**: Package your coding standards, architectural patterns, and best practices as skills that AI assistants can reference.

{: .important }
> **Framework Guidance**: Ship your SDK or library with AI skills that teach developers how to use it correctly.

{: .note }
> **Team Knowledge**: Capture tribal knowledge and domain expertise as distributable AI skills.

---

## Getting Help

- [Getting Started Guide](/getting-started) - First steps with Imprint
- [Concepts](/concepts/overview) - Understand how Imprint works
- [Guides](/guides/creating-skill-packages) - Step-by-step tutorials
- [Reference](/reference/configuration) - Complete configuration reference
- [Troubleshooting](/troubleshooting) - Common issues and solutions

---

## License

Zakira.Imprint is distributed under the [MIT License](https://github.com/MoaidHathot/Zakira.Imprint/blob/main/LICENSE).
