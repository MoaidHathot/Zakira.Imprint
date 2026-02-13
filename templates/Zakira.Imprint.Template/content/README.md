# Imprint.MySkills

PACKAGE_DESCRIPTION

## Installation

```bash
dotnet add package Imprint.MySkills
dotnet build
```

Skills are automatically installed to `.github/skills/` in your project on build.

## Included Skills

| Skill | Description |
|-------|-------------|
| **SKILL-Example.md** | Example skill file - replace with your actual skills |

## How It Works

This package uses the [Imprint](https://github.com/MoaidHathot/SkillsViaNuget) pattern:

1. **On `dotnet build`**: Skills are copied to `.github/skills/`
2. **On `dotnet clean`**: Skills are removed
3. **On package update**: Old skills are replaced with new versions

## Development

### Adding New Skills

1. Create new `.md` files in the `skills/` folder
2. Follow the naming convention: `SKILL-TopicName.md`
3. Bump the version in `Imprint.MySkills.csproj` (`<Version>`)

### Testing Locally

```bash
# Pack the package
dotnet pack -o ./packages

# In a test project, add nuget.config pointing to ./packages
# Then reference the package and build
dotnet build
```

### Publishing

```bash
dotnet pack -c Release -o ./packages
dotnet nuget push ./packages/Imprint.MySkills.1.0.0.nupkg -s https://api.nuget.org/v3/index.json -k YOUR_API_KEY
```

## License

MIT
