# Zakira.Imprint.Template

A `dotnet new` template for creating Imprint AI Skills packages.

## Installation

```bash
dotnet new install Zakira.Imprint.Template
```

Or install from a local `.nupkg` file:

```bash
dotnet new install ./Zakira.Imprint.Template.1.0.0.nupkg
```

## Usage

Create a new Imprint package:

```bash
# Basic usage (uses folder name as package ID)
mkdir MyOrg.CodingStandards
cd MyOrg.CodingStandards
dotnet new imprint

# With custom parameters
dotnet new imprint -n MyOrg.CodingStandards --author "My Name" --description "Coding standards for my organization"
```

## Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-n, --name` | Package ID and project name | `Imprint.MySkills` |
| `--author` | Package author | `Your Name` |
| `--description` | Package description | `AI Skills for your domain.` |
| `--version` | Initial package version | `1.0.0` |

## What's Generated

```
MyOrg.CodingStandards/
├── MyOrg.CodingStandards.csproj       # NuGet package definition
├── build/
│   └── MyOrg.CodingStandards.targets  # MSBuild targets (declares items for Zakira.Imprint.Sdk)
├── mcp/
│   └── MyOrg.CodingStandards.mcp.json # MCP server fragment (optional)
├── skills/
│   └── SKILL-Example.md              # Example skill file
└── README.md                          # Package documentation
```

The generated package references `Zakira.Imprint.Sdk` which provides all MSBuild task logic (content copying, cleaning, MCP merging). Your `.targets` file only needs ~25 lines to declare what content to copy.

## Next Steps After Generation

1. **Edit skills**: Replace `SKILL-Example.md` with your actual skills
2. **Add more skills**: Create additional `.md` files in the `skills/` folder
3. **Update metadata**: Edit the `.csproj` for description, tags, etc.
4. **Pack**: `dotnet pack -o ./packages`
5. **Test locally**: Reference the package from a test project
6. **Publish**: `dotnet nuget push` to NuGet.org

## Uninstalling

```bash
dotnet new uninstall Zakira.Imprint.Template
```
