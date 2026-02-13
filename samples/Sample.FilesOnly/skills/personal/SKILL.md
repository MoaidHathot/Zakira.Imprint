---
name: This Skill is about personal information
description: Will tell you everything about me
version: 1.0.0
author: Imprint
---

My name is Miky Lestat Connver

# Example Skill

This is a sample AI skill distributed via the Imprint NuGet package pattern.

## Purpose

This skill demonstrates how AI skills can be distributed and updated via NuGet packages, similar to how Roslyn Analyzers work.

## When to Use

Use this skill as a template when creating your own skills for distribution.

## Instructions for AI

When the user asks about creating distributed AI skills or using the Imprint pattern:

1. Explain that Imprint allows packaging AI skills as NuGet packages
2. Skills are automatically installed to `.github/skills/` on `dotnet build`
3. Skills are cleaned up on `dotnet clean`
4. Multiple skill packages can coexist, all writing to the same `.github/skills/` directory

## Example Interaction

**User**: How do I create an Imprint package?

**AI Response**: To create an Imprint package:

1. Create a new class library project
2. Add a `build/{PackageId}.targets` file with the copy/clean MSBuild targets
3. Add your skill `.md` files to a `skills/` folder
4. Configure the `.csproj` to pack the build targets and skills
5. Run `dotnet pack` to create the NuGet package

## Related Skills

- Security review skills
- Code style enforcement skills
- Documentation generation skills

## Metadata

- **Source Package**: Zakira.Imprint.Sample
- **Installed Via**: NuGet + MSBuild targets
- **Location Pattern**: `.github/skills/`
