---
layout: default
title: Manifest System
parent: Concepts
nav_order: 5
description: "How Imprint tracks installed files and managed MCP servers."
permalink: /concepts/manifest
---

# Manifest System
{: .fs-9 }

How Imprint tracks what it installs for reliable cleanup.
{: .fs-6 .fw-300 }

---

## Overview

Imprint uses a manifest system to track:

- Which files were installed by which package
- Which agents were targeted
- Which MCP servers are managed

This tracking enables clean operations to reliably remove exactly what was installed.

---

## Unified Manifest (v2)

The primary manifest is `.imprint/manifest.json`:

```json
{
  "version": 2,
  "packages": {
    "MyOrg.Skills.Security": {
      "files": {
        "copilot": [
          ".github/skills/authentication/SKILL.md",
          ".github/skills/input-validation/SKILL.md"
        ],
        "claude": [
          ".claude/skills/authentication/SKILL.md",
          ".claude/skills/input-validation/SKILL.md"
        ]
      }
    },
    "MyOrg.Skills.Coding": {
      "files": {
        "copilot": [
          ".github/skills/naming/SKILL.md"
        ],
        "claude": [
          ".claude/skills/naming/SKILL.md"
        ]
      }
    }
  },
  "mcp": {
    "copilot": {
      "path": ".vscode/mcp.json",
      "managedServers": ["security-scanner", "code-analyzer"]
    },
    "claude": {
      "path": ".claude/mcp.json",
      "managedServers": ["security-scanner", "code-analyzer"]
    }
  }
}
```

### Manifest Location

```
ProjectDirectory/
├── .imprint/
│   ├── manifest.json      # Unified manifest
│   └── .gitignore         # Contains "*" to ignore all
└── MyProject.csproj
```

### Why `.imprint/.gitignore` Contains `*`

The manifest is generated on build and should not be committed. It contains absolute paths that may differ between machines.

---

## Manifest Structure

### Version

```json
{
  "version": 2
}
```

The version number identifies the manifest format. Version 2 was introduced in SDK 1.1.0 with multi-agent support.

### Packages Section

```json
{
  "packages": {
    "PackageId": {
      "files": {
        "agent-name": ["array", "of", "file", "paths"]
      }
    }
  }
}
```

Each package entry tracks:
- **PackageId**: The NuGet package that installed these files
- **files**: A dictionary of agent name → file paths
- **File paths**: Relative to the project directory

### MCP Section

```json
{
  "mcp": {
    "agent-name": {
      "path": "relative/path/to/mcp.json",
      "managedServers": ["server-name-1", "server-name-2"]
    }
  }
}
```

Each MCP entry tracks:
- **path**: Where the agent's `mcp.json` is located
- **managedServers**: Server keys that Imprint manages

---

## How the Manifest is Used

### On Build

1. Copy skills to agent directories
2. Update `packages` section with file paths
3. Merge MCP servers
4. Update `mcp` section with managed server keys
5. Write updated manifest

### On Clean

1. Read manifest
2. For each package, for each agent: delete tracked files
3. For each agent in MCP: remove managed servers from `mcp.json`
4. Delete empty directories
5. Delete the manifest if everything is removed

---

## Legacy Manifests

For backward compatibility, Imprint also writes legacy per-package manifests:

```
ProjectDirectory/
├── .imprint/
│   ├── manifest.json                    # Unified (v2)
│   ├── MyOrg.Skills.Security.manifest   # Legacy (v1)
│   └── MyOrg.Skills.Coding.manifest     # Legacy (v1)
```

Legacy format:
```
.github/skills/authentication/SKILL.md
.github/skills/input-validation/SKILL.md
.claude/skills/authentication/SKILL.md
.claude/skills/input-validation/SKILL.md
```

### When Legacy Manifests Are Used

- Cleaning with older SDK versions
- Projects with mixed SDK versions
- Fallback if unified manifest is missing

---

## Clean Without Manifest

If the manifest is deleted or corrupted, clean behavior changes:

1. **No files are deleted** - Imprint doesn't know what to clean
2. **MCP servers remain** - No tracking of managed servers
3. **Manual cleanup required** - User must delete files manually

{: .warning }
> Don't delete `.imprint/manifest.json` manually. If you need to reset, run `dotnet clean` first.

---

## Manifest and Source Control

### What to Commit

| Item | Commit? | Reason |
|:-----|:--------|:-------|
| `.imprint/` | No | Contains machine-specific paths |
| Skill files | Optional | May want team consistency |
| `mcp.json` | Optional | May want team consistency |

### Recommended `.gitignore`

```gitignore
# Imprint manifest (auto-generated on build)
.imprint/

# Optional: also ignore generated skills
# .github/skills/
# .claude/skills/
# .cursor/rules/
```

---

## Debugging Manifest Issues

### View Current Manifest

```bash
cat .imprint/manifest.json
```

### Verify File Tracking

Check if a specific file is tracked:

```bash
# PowerShell
Get-Content .imprint/manifest.json | Select-String "SKILL.md"

# Bash
grep "SKILL.md" .imprint/manifest.json
```

### Force Rebuild

If the manifest is out of sync:

```bash
# Clean everything
dotnet clean

# Delete manifest manually if clean didn't work
rm -rf .imprint/

# Rebuild
dotnet build
```

---

## Manifest Evolution

### v1 (SDK 1.0.0)

- Per-package manifest files
- Single agent support (Copilot only)
- No MCP tracking

### v2 (SDK 1.1.0)

- Unified manifest
- Multi-agent support
- MCP server tracking
- Backward-compatible with v1

### Future Versions

The manifest format may evolve. The SDK will always:
- Migrate older manifests automatically
- Support reading older formats
- Write the latest format

---

## Next Steps

- [Architecture Reference](/reference/architecture) - Deep dive into internals
- [Troubleshooting](/troubleshooting) - Common manifest issues
