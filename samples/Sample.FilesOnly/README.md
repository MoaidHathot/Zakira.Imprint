# Zakira.Imprint.Sample

Sample AI Skills package for GitHub Copilot and other AI assistants.

## Installation

```bash
dotnet add package Zakira.Imprint.Sample
dotnet build
```

## What happens on build?

When you add this package and run `dotnet build`, the included AI skills are automatically copied to `.github/skills/` in your project directory, preserving folder structure.

## Included Skills

- **personal/SKILL.md** - A sample skill demonstrating the format
- **personal/scripts/testScript.ps1** - A sample script demonstrating non-markdown file support

## Configuration

By default, skills are installed to `.github/skills/`. You can customize this by setting the `AISkillsBasePath` property in your project file:

```xml
<PropertyGroup>
  <AISkillsBasePath>$(MSBuildProjectDirectory)\.copilot\skills\</AISkillsBasePath>
</PropertyGroup>
```

## Cleaning Up

Run `dotnet clean` to remove the installed skills. They will be restored on the next `dotnet build`.

## Git Integration

A shared `.gitignore` file is automatically created at `.github/skills/.gitignore`, so no manual `.gitignore` configuration is needed.
