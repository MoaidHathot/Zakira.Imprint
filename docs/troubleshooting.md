---
layout: default
title: Troubleshooting
nav_order: 7
---

# Troubleshooting

Common issues and solutions when working with Zakira.Imprint.

---

## Build-Time Issues

### Skills Not Being Packaged

**Symptom:** Your NuGet package doesn't contain the expected skill files.

**Possible causes:**

1. **Missing `<Imprint>` items in csproj:**
   ```xml
   <!-- Ensure you have Imprint items defined -->
   <ItemGroup>
     <Imprint Include="skills\**\*" />
   </ItemGroup>
   ```

2. **Incorrect glob patterns:**
   ```xml
   <!-- Wrong: Missing recursive pattern -->
   <Imprint Include="skills\*" />
   
   <!-- Correct: Include all files recursively -->
   <Imprint Include="skills\**\*" />
   ```

3. **Files excluded by .gitignore patterns:**
   - Check if your skill files match patterns in `.gitignore`
   - The SDK respects standard ignore patterns

**Diagnosis:**
```bash
# Build with detailed logging
dotnet pack -v detailed | grep -i imprint
```

### Generated .targets File Missing

**Symptom:** The `.targets` file isn't generated in the NuGet package.

**Possible causes:**

1. **No `<Imprint>` items defined** - At least one item is required
2. **Build errors preventing target generation** - Check build output

**Solution:**
```bash
# Verify targets generation
dotnet pack
# Check the nupkg contents
unzip -l bin/Debug/*.nupkg | grep targets
```

### MCP Server Configuration Not Generated

**Symptom:** MCP server entries aren't appearing in the agent's config file.

**Possible causes:**

1. **Missing MCP fragment file:**
   ```xml
   <!-- MCP servers are defined via .mcp.json fragment files -->
   <ItemGroup>
     <Imprint Include="skills\**\*" />
     <Imprint Include="mcp\*.mcp.json" Type="Mcp" />
   </ItemGroup>
   ```

2. **Fragment file not in mcp folder:**
   - Create a `mcp/` folder in your package project
   - Add a `{name}.mcp.json` file with your server definition:
   ```json
   {
     "servers": {
       "my-server": {
         "command": "dotnet",
         "args": ["tool", "run", "my-mcp-server"]
       }
     }
   }
   ```

---

## Restore-Time Issues

### Skills Not Deployed After Package Restore

**Symptom:** After `dotnet restore`, skill files don't appear in expected locations.

**Possible causes:**

1. **Missing implicit usings for SDK:**
   ```xml
   <!-- In consuming project -->
   <PropertyGroup>
     <ImplicitUsings>enable</ImplicitUsings>
   </PropertyGroup>
   ```

2. **Package not properly restored:**
   ```bash
   # Force a clean restore
   dotnet restore --force
   ```

3. **Targets not imported:**
   - Ensure the package reference is correct
   - Check that the package contains the `.targets` file

**Diagnosis:**
```bash
# Verify package contents
dotnet nuget locals all --list
# Navigate to the package cache and inspect the .targets file
```

### Files Deployed to Wrong Location

**Symptom:** Skill files appear but in unexpected directories.

**Understanding the expected locations:**

| Agent | Expected Path |
|-------|---------------|
| GitHub Copilot | `.github/skills/{package-folder}/SKILL.md` |
| Claude | `.claude/skills/{package-folder}/SKILL.md` |
| Cursor | `.cursor/rules/{package-folder}/*.mdc` |
| Roo Code | `.roo/rules/{package-folder}/*.mdc` |

**Possible causes:**

1. **Custom skills path set:**
   ```xml
   <!-- Check if this is intentionally set -->
   <PropertyGroup>
     <ImprintSkillsPath>$(MSBuildProjectDirectory)\custom\skills\</ImprintSkillsPath>
   </PropertyGroup>
   ```

2. **Wrong agent targeted:**
   - The skill folder varies by agent (`.github/skills/`, `.claude/skills/`, `.cursor/rules/`, `.roo/rules/`)
   - Verify `ImprintTargetAgents` is set correctly

### Manifest Conflicts

**Symptom:** Error about manifest conflicts or version mismatches.

**Solution:**
```bash
# Remove the manifest and let it regenerate
rm .imprint/manifest.json
dotnet restore
```

**Understanding manifests:**
- Location: `.imprint/manifest.json`
- Tracks all deployed files per package
- Version 2 format includes MCP server entries

---

## MCP Server Issues

### MCP Server Not Appearing in Agent Config

**Symptom:** After restore, MCP server isn't configured in the agent's settings file.

**Expected locations:**

| Agent | Config File | Root Key |
|-------|-------------|----------|
| Copilot (VS Code) | `.vscode/mcp.json` | `servers` |
| Claude | `.claude/mcp.json` | `mcpServers` |
| Cursor | `.cursor/mcp.json` | `mcpServers` |
| Roo Code | `.roo/mcp.json` | `mcpServers` |

**Possible causes:**

1. **Missing MCP configuration in package:**
   ```xml
   <!-- MCP servers are configured via fragment files -->
   <ItemGroup>
     <Imprint Include="mcp\*.mcp.json" Type="Mcp" />
   </ItemGroup>
   ```
   
   And create a fragment file (e.g., `mcp/myserver.mcp.json`):
   ```json
   {
     "servers": {
       "my-mcp-server": {
         "command": "npx",
         "args": ["-y", "@myorg/mcp-server"]
       }
     }
   }
   ```

2. **Agent not targeted:**
   ```xml
   <!-- Ensure the agent is included -->
   <PropertyGroup>
     <ImprintTargetAgents>copilot;claude;cursor;roo</ImprintTargetAgents>
   </PropertyGroup>
   ```

### MCP Server Entry Not Removed on Clean

**Symptom:** After removing a package reference, MCP entries remain.

**Solution:**
```bash
# Run a clean build
dotnet clean
dotnet restore
```

**Manual cleanup:**
1. Open the relevant config file (e.g., `.vscode/mcp.json`)
2. Remove the orphaned server entry
3. The manifest tracks what should be cleaned

### JSON Merge Conflicts

**Symptom:** MCP config file has invalid JSON or merge errors.

**Solution:**
1. Backup the current config file
2. Delete the config file
3. Run `dotnet restore` to regenerate

```bash
# Example for VS Code
mv .vscode/mcp.json .vscode/mcp.json.backup
dotnet restore
```

---

## Agent-Specific Issues

### GitHub Copilot Not Finding Skills

**Symptom:** Copilot doesn't recognize or use the deployed skills.

**Checklist:**
1. Verify files exist in `.github/skills/`
2. Ensure `SKILL.md` files are present and properly formatted
3. Restart VS Code / reload the Copilot extension
4. Check that Copilot has indexed the workspace

### Claude Not Loading Skills

**Symptom:** Claude doesn't recognize the deployed skills.

**Checklist:**
1. Verify files exist in `.claude/skills/`
2. Check `.claude/mcp.json` for MCP configuration
3. Restart Claude Desktop application
4. Verify MCP server is running (if applicable)

### Cursor Rules Not Applied

**Symptom:** Cursor doesn't apply the deployed rules.

**Checklist:**
1. Verify `.mdc` files exist in `.cursor/rules/`
2. Restart Cursor IDE
3. Check Cursor's rules panel for the loaded rules

### Roo Code Rules Not Applied

**Symptom:** Roo Code doesn't apply the deployed rules.

**Checklist:**
1. Verify rule files exist in `.roo/rules/`
2. Check `.roo/mcp.json` for MCP configuration
3. Restart Roo Code
4. Verify MCP server is running (if applicable)

---

## Package Publishing Issues

### Package Rejected by NuGet.org

**Symptom:** Push to NuGet.org fails with validation errors.

**Common causes:**

1. **Missing required metadata:**
   ```xml
   <PropertyGroup>
     <PackageId>YourPackageId</PackageId>
     <Version>1.0.0</Version>
     <Authors>Your Name</Authors>
     <Description>Package description</Description>
     <PackageLicenseExpression>MIT</PackageLicenseExpression>
     <PackageProjectUrl>https://github.com/you/project</PackageProjectUrl>
   </PropertyGroup>
   ```

2. **Package ID already taken:**
   - Choose a unique package ID
   - Consider using a prefix like your organization name

3. **Invalid version number:**
   - Use semantic versioning (e.g., `1.0.0`, `1.0.0-beta.1`)

### Large Package Size

**Symptom:** Package is unexpectedly large.

**Diagnosis:**
```bash
# List package contents with sizes
unzip -l bin/Debug/*.nupkg
```

**Solutions:**
1. Exclude unnecessary files:
   ```xml
   <ItemGroup>
     <Imprint Include="skills\**\*" Exclude="**\*.tmp;**\node_modules\**" />
   </ItemGroup>
   ```

2. Review what's being included
3. Consider splitting into multiple packages

---

## Development Workflow Issues

### Changes Not Reflected After Rebuild

**Symptom:** Skill file changes don't appear after rebuilding.

**Solutions:**

1. **Force package regeneration:**
   ```bash
   dotnet clean
   dotnet pack
   ```

2. **Clear local package cache:**
   ```bash
   dotnet nuget locals all --clear
   ```

3. **Update package version:**
   - Increment the version number
   - NuGet caches packages by version

### Local Testing Not Working

**Symptom:** Can't test packages locally before publishing.

**Solution - Use local feed:**
```bash
# Create local feed directory
mkdir ~/local-nuget-feed

# Pack and copy to local feed
dotnet pack -o ~/local-nuget-feed

# Add local source (one-time)
dotnet nuget add source ~/local-nuget-feed --name local

# In consuming project
dotnet add package YourPackage --source local
```

---

## Diagnostic Commands

### Verbose Build Output

```bash
# MSBuild detailed logging
dotnet build -v detailed 2>&1 | tee build.log

# Filter for Imprint-related output
grep -i imprint build.log
```

### Inspect Package Contents

```bash
# List all files in the package
unzip -l MyPackage.1.0.0.nupkg

# Extract and inspect
unzip MyPackage.1.0.0.nupkg -d package-contents/
cat package-contents/build/MyPackage.targets
```

### Verify Manifest State

```bash
# View current manifest
cat .imprint/manifest.json | jq .

# Check manifest version
cat .imprint/manifest.json | jq .version
```

### Check Agent Configurations

```bash
# Copilot MCP config (VS Code)
cat .vscode/mcp.json | jq .

# Claude config
cat .claude/mcp.json | jq .

# Cursor MCP config
cat .cursor/mcp.json | jq .

# Roo Code MCP config
cat .roo/mcp.json | jq .
```

---

## Getting Help

If you're still experiencing issues:

1. **Check the GitHub Issues:** [github.com/zakira/imprint/issues](https://github.com/zakira/imprint/issues)
2. **Enable verbose logging** and include the output in your issue
3. **Include your project structure** and relevant csproj snippets
4. **Specify the SDK version** you're using

### Reporting a Bug

When reporting issues, please include:

- Zakira.Imprint.Sdk version
- .NET SDK version (`dotnet --version`)
- Operating system
- Relevant csproj configuration
- Build/restore output with verbose logging
- Expected vs actual behavior
