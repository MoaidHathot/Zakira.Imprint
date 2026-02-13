# Distributing AI Skills and Custom Instructions via NuGet

As developers, we are always looking for ways to share knowledge and improve productivity. With the rise of AI coding assistants like GitHub Copilot, Claude, and others, a new category of files has emerged: Skills, AGENTS.MD, custom instructions, and other AI-related configuration files. These files help AI agents understand our codebase, our patterns, and our preferences.

A few weeks ago, I found myself in a situation where I needed to share a set of Skills and custom instructions between multiple projects. I was also working on an internal library that LLMs simply did not know about - and I needed a way to teach them.

## The Problem

It all started with a simple requirement: I wanted to share a set of AI Skills across multiple projects. At first, I was manually copying files between repositories. This worked fine for a while, but it quickly became a maintenance burden. Every time I updated a skill, I had to remember which projects were using it and manually update each one.

On top of that, I was developing an internal library. LLMs do not have training data for internal libraries (obviously), and while they can sometimes decompile and understand APIs, they often miss the bigger picture - the why behind the library, the common patterns, the pitfalls to avoid, and the best practices.

Sure, if the documentation exists somewhere, you can sometimes point the AI to it. But internal documentation is often not indexed by AI assistants, and even if it is, the AI might not know where to look. This is especially true for proprietary libraries, where decompiling the code can help with understanding the API surface, but not with the broader context.

### What if I Could Ship AI Skills with the Library Itself?

This is when I had the idea: what if library authors could ship Skills, custom instructions, or even AGENTS.MD files alongside their NuGet packages? This way, when you install a library, the AI assistant would automatically get the context it needs to use that library correctly.

This is not a new concept. We have been doing something similar with Roslyn Analyzers for years - you install a NuGet package, and you get code analysis rules that guide your coding. Why not do the same for AI assistants?

## Enter Imprint

Imprint is a pattern (and a set of packages) that enables distributing AI Skills via NuGet. The concept is simple:

1. Package your AI Skills (markdown files, custom instructions, scripts, and any other files) as a NuGet package
2. When someone adds your package to their project, the skills are automatically copied to `.github/skills/`
3. When the package is updated, the skills are updated on the next build
4. When the package is removed and the project is cleaned, the skills are removed

This approach brings several benefits that I have found invaluable.

### Easy to Ship

Instead of manually copying files or maintaining shared repositories, you just pack your skills into a NuGet package. Anyone who wants to use them just adds a package reference:

```bash
dotnet add package Zakira.Imprint.AzureBestPractices
dotnet build
```

That is it. The skills are now installed and ready for AI assistants to use.

### Easy to Update

When you publish a new version of your skills package, consumers just need to update their package reference. The MSBuild targets detect the version change and automatically replace the old skills with the new ones:

```bash
dotnet add package Zakira.Imprint.AzureBestPractices --version 2.0.0
dotnet build
# Skills are automatically updated!
```

No more "did you remember to copy the new skills?" conversations.

### Library Authors Can Teach AI About Their Libraries

This is the part I am most excited about. If you are authoring a library - whether internal or public - you can now ship AI instructions alongside it. Your users install your library, and their AI assistant immediately knows:

- How to use your APIs correctly
- Common patterns and best practices
- Pitfalls to avoid
- Migration guides between versions

For internal libraries where LLMs have no training data, this is a game changer. Instead of the AI guessing (often incorrectly) how to use your library, it gets explicit guidance directly from the library authors.

### No Code Changes Required

Here is the nice part: the skills are installed to `.github/skills/` and a shared `.gitignore` is automatically generated at the root of the skills folder. No manual `.gitignore` configuration is needed.

This means:
- No code changes to commit
- Skills are regenerated on every build
- Different developers can have different skill packages installed
- CI/CD environments get fresh skills on every build

## How It Works

The mechanism is similar to how Roslyn Analyzers are distributed. Let me break it down.

### The Architecture: Zakira.Imprint.Sdk

At the heart of Imprint is a shared engine package called `Zakira.Imprint.Sdk`. This package contains compiled MSBuild tasks that handle all the heavy lifting: copying skill files, managing manifests, merging MCP configurations, and cleaning up. Individual skill packages do not duplicate any of this logic — they simply declare what content they ship, and the SDK handles the rest.

### The Package Structure

An Imprint package contains:
- `build/{PackageId}.targets` - Declares content items (skills and MCP fragments)
- `skills/**/*` - The actual skill files (any file type, preserving folder structure)
- `mcp/{PackageId}.mcp.json` - Optional MCP server fragment

The package also takes a dependency on `Zakira.Imprint.Sdk`, which provides the MSBuild tasks that process these declarations.

### NuGet Restore and MSBuild Integration

When NuGet restores a package that contains a `build/{PackageId}.targets` file, MSBuild automatically imports it. This is a standard NuGet behavior - nothing special here. The `Zakira.Imprint.Sdk` package uses the `buildTransitive/` folder convention so its targets are imported transitively through skill packages.

### How Packages Declare Content

Each skill package's `.targets` file is minimal — typically around 25 lines. It declares two kinds of items:

```xml
<Project>
  <PropertyGroup>
    <_Imprint_MyPkg_Root>$(MSBuildThisFileDirectory)..\</_Imprint_MyPkg_Root>
  </PropertyGroup>

  <!-- Skill files to copy -->
  <ItemGroup>
    <ImprintContent Include="$(_Imprint_MyPkg_Root)skills\**\*">
      <DestinationBase>$(ImprintSkillsPath)</DestinationBase>
      <PackageId>Zakira.Imprint.MyPkg</PackageId>
      <SourceBase>$(_Imprint_MyPkg_Root)skills\</SourceBase>
    </ImprintContent>
  </ItemGroup>

  <!-- MCP server fragments -->
  <ItemGroup>
    <ImprintMcpFragment Include="$(_Imprint_MyPkg_Root)mcp\*.mcp.json" />
  </ItemGroup>
</Project>
```

The `Zakira.Imprint.Sdk` targets then process all `ImprintContent` and `ImprintMcpFragment` items across all installed packages.

### Target Execution

The SDK hooks into the build lifecycle with four targets:

- **Imprint_CopyContent** (BeforeTargets="BeforeBuild") — Copies all declared skill files, writes per-package manifests to `.imprint/`, creates `.gitignore`
- **Imprint_CleanContent** (AfterTargets="Clean") — Reads manifests, deletes only tracked files, removes empty directories
- **Imprint_MergeMcp** (BeforeTargets="BeforeBuild") — Merges all MCP fragments into `.vscode/mcp.json`
- **Imprint_CleanMcp** (AfterTargets="Clean") — Removes managed MCP servers, preserves user-defined ones

The key points:
- Skills are copied **before every build** (skipping design-time builds for IDE performance)
- All file types are included, preserving folder structure from the `skills/` directory
- A shared `.gitignore` at the skills root prevents files from being committed
- Per-package manifests (`.imprint/{PackageId}.manifest`) track exactly which files each package installed
- Skills are cleaned up with `dotnet clean` — only the specific files from each package are removed

### Multi-Package Support

Multiple packages can install skills into the same `.github/skills/` folder. Each package's skill files are copied preserving their folder structure from the `skills/` directory within the package:

```
.github/
  skills/
    .gitignore
    SKILL-AzureSecurity.md
    StringUtils/
      SKILL.md
    personal/
      SKILL.md
```

On clean, the manifest-based tracking ensures each package only removes the specific files it installed, so multiple packages coexist safely.

## MCP Server Injection: Beyond Skills

After building the skills distribution, I realized there was another piece of the puzzle missing. Modern AI assistants do not just consume static files — they connect to MCP (Model Context Protocol) servers that provide dynamic tools, resources, and prompts. VS Code discovers these servers through a `.vscode/mcp.json` file.

What if an Imprint package could also configure MCP servers? Instead of asking users to manually edit their `mcp.json`, the package would inject the right server configuration at build time.

### How MCP Injection Works

The approach follows the same philosophy as skills distribution: install a package, build, and everything is configured for you.

Each Imprint package that ships an MCP server includes a **fragment file** — a small JSON file containing its server definitions:

```json
{
  "servers": {
    "azure-mcp-server": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@anthropic-ai/azure-mcp-server"]
    }
  }
}
```

At build time, the `Zakira.Imprint.Sdk` engine collects all fragment files from installed packages and merges them into `.vscode/mcp.json`. The result is a single file that VS Code reads to discover all available MCP servers.

### The Hard Part: Not Breaking User Configuration

The tricky part is not the merge itself — it is knowing what to keep and what to remove. Users might have their own servers defined in `mcp.json`. If an Imprint package is removed, only its servers should be cleaned up. Other servers, including user-defined ones, must survive.

To solve this, I introduced a **manifest file** (`.vscode/.imprint-mcp-manifest`) that tracks which server keys are managed by Imprint. This file is automatically gitignored, while `mcp.json` itself can be committed to source control.

The manifest approach is better than the alternative I considered — using naming conventions like `imprint-auto-*` prefixes on server keys. With a manifest, packages can use natural names like `azure-mcp-server` instead of `imprint-auto-azure-mcp-server`.

### Idempotent and Safe

The merge logic is idempotent: if nothing changed since the last build, `mcp.json` is not rewritten. This means no unnecessary git diffs. On `dotnet clean`, only managed servers are removed. If the file has no remaining content after cleanup, it is deleted entirely.

Top-level properties like `"inputs"` (used by VS Code for secret prompts) are preserved through all operations. Your hand-crafted configuration is never touched.

### A Compiled MSBuild Task

One technical challenge worth mentioning: I initially tried to implement the merge logic as an inline C# task using `RoslynCodeTaskFactory`. It turns out this approach cannot reference `System.Text.Json`, which is needed for proper JSON merging. The solution was to create compiled MSBuild task DLLs. All tasks — content copying, cleaning, MCP merging, and MCP cleaning — live in a single shared package called `Zakira.Imprint.Sdk`. Skill packages reference it as a dependency, and NuGet handles the transitive import of the build targets via the `buildTransitive/` folder.

This centralized approach means each skill package's `.targets` file is only ~25 lines of item declarations, with zero duplicated MSBuild logic.

### Adding MCP to Your Package

If you want your Imprint package to inject MCP servers, add two things:

1. A `mcp/<PackageId>.mcp.json` fragment file with your server definitions
2. An `ImprintMcpFragment` item group in your `.targets` file pointing to the fragment

That is it. The `Zakira.Imprint.Sdk` dependency (which your package already has for skill distribution) handles the merge automatically. When a consumer installs your package and builds, they get both the AI skills and the MCP server configuration.

## Finding the Right Balance

One concern I had when designing this was overcrowding. What if every NuGet package starts shipping AI skills? Your `.github/skills/` folder could become cluttered with files you do not need.

The solution is simple: these are development dependencies. They are marked as `PrivateAssets="all"` in the package reference, meaning they do not flow to downstream projects. And since a shared `.gitignore` is placed at the skills root, they do not bloat your repository.

For library authors, I would recommend being intentional about what you include. Ship skills that genuinely help users of your library. Do not include generic programming advice that AI already knows.

If you want to control which skill packages are installed, you can simply not reference the ones you do not need. It is opt-in by design.

## Two Package Patterns

Imprint supports two patterns for package authors:

### Skills-Only Packages

These packages ship only AI skills and MCP configurations — no compiled library code. They are development-time dependencies that leave no trace in the consumer's build output:

```xml
<IncludeBuildOutput>false</IncludeBuildOutput>
<DevelopmentDependency>true</DevelopmentDependency>
```

Example: `Zakira.Imprint.AzureBestPractices` ships Azure security skills without any runtime DLL.

### Library + Skills Packages

These packages ship a compiled DLL **and** AI skills. The DLL is a real runtime dependency, while the skills teach AI assistants how to use the library correctly:

```xml
<!-- No IncludeBuildOutput=false — the DLL ships in lib/ -->
<!-- No DevelopmentDependency=true — the package is needed at runtime -->
```

Example: `Zakira.Imprint.Sample.WithCode` ships string utility extension methods alongside a skill file that teaches AI the correct usage patterns, common pitfalls, and best practices.

This is the pattern I am most excited about for internal libraries. Your consumers get the library and the AI guidance in a single `dotnet add package`.

## Creating Your Own Imprint Package

If you want to create your own skills package, you have two options:

### Option 1: Use the Template

```bash
# Install the template
dotnet new install Zakira.Imprint.Template

# Create a new package
dotnet new imprint -n MyOrg.CodingStandards

# Add your skills to the skills/ folder
# Then pack and publish
dotnet pack
```

### Option 2: Manual Setup

Create a project with the following structure:

```
MyOrg.Skills/
  MyOrg.Skills.csproj
  build/
    MyOrg.Skills.targets
  skills/
    SKILL-YourSkill.md
  mcp/
    MyOrg.Skills.mcp.json    (optional)
```

Configure the `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <PackageId>MyOrg.Skills</PackageId>
    
    <!-- Tools-only package (omit these for library + skills packages) -->
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference Zakira.Imprint.Sdk for MSBuild task engine -->
    <PackageReference Include="Zakira.Imprint.Sdk" Version="1.0.0">
      <PrivateAssets>compile</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <None Include="build\**" Pack="true" PackagePath="build" />
    <None Include="skills\**\*" Pack="true" PackagePath="skills" />
    <None Include="mcp\**\*" Pack="true" PackagePath="mcp" />
  </ItemGroup>
</Project>
```

The `.targets` file declares items for `Zakira.Imprint.Sdk` to process. Check the repository for a complete example.

## Use Cases

I have found this pattern useful for several scenarios:

**Organization-wide Standards**: Package your company's coding standards, security guidelines, and architectural patterns as skills. Every project that references the package gets consistent guidance.

**Framework Best Practices**: Create a package with best practices for specific frameworks. For example, `Zakira.Imprint.AzureBestPractices` includes guidance on Azure SDK usage, resource naming, and security patterns.

**Internal Library Documentation**: Ship your internal library with skills that teach AI how to use it. With the library + skills pattern, your consumers get the DLL and the AI guidance in a single package install. This is especially valuable for complex libraries with non-obvious usage patterns.

**MCP Server Distribution**: Ship MCP server configurations alongside your skills. Consumers get both static knowledge (skills) and dynamic tooling (MCP servers) from a single NuGet package install.

**Team Knowledge Sharing**: Package tribal knowledge that would otherwise live in wiki pages or developers' heads. Make it available to AI assistants so they can help new team members.

## Multi-Agent Support: Beyond Copilot

The initial version of Imprint targeted only GitHub Copilot, placing skills in `.github/skills/` and MCP configs in `.vscode/mcp.json`. But AI assistants are not a monoculture. Teams use Claude, Cursor, and increasingly other tools alongside Copilot. Maintaining separate skill files for each agent is the same copy-paste problem Imprint was built to solve.

With v1.1.0, Imprint gained multi-agent support. A single NuGet package now distributes skills and MCP configurations to **every AI agent simultaneously**, placing files in each agent's native directory structure.

### How It Works

Each AI agent has its own conventions for where it looks for skills and MCP configurations:

| Agent | Skills Path | MCP Path |
|-------|-------------|----------|
| Copilot | `.github/skills/` | `.vscode/mcp.json` |
| Claude | `.claude/skills/` | `.claude/mcp.json` |
| Cursor | `.cursor/rules/` | `.cursor/mcp.json` |

Imprint auto-detects which agents you use by scanning for their configuration directories. If `.github/` and `.claude/` both exist, Imprint targets both. The same skill content is copied to each agent's native location, and MCP server configs are merged into each agent's `mcp.json`.

### Zero Configuration Required

The default behavior is auto-detection. You do not need to change anything — Imprint looks for `.github/`, `.claude/`, and `.cursor/` directories at build time and targets whichever agents are present. If none are detected, it falls back to Copilot as the default.

If you want explicit control, set a single MSBuild property:

```xml
<PropertyGroup>
  <ImprintTargetAgents>claude;cursor</ImprintTargetAgents>
</PropertyGroup>
```

### Unified Manifest

With multiple agents, tracking which files belong to which agent becomes important for clean operations. Imprint v1.1.0 introduced a unified manifest format (v2) at `.imprint/manifest.json` that tracks everything in one place:

```json
{
  "version": 2,
  "packages": {
    "Zakira.Imprint.Sample": {
      "files": {
        "copilot": [".github/skills/personal/SKILL.md"],
        "claude": [".claude/skills/personal/SKILL.md"]
      }
    }
  },
  "mcp": {
    "copilot": {
      "path": ".vscode/mcp.json",
      "managedServers": ["sample-echo-server"]
    },
    "claude": {
      "path": ".claude/mcp.json",
      "managedServers": ["sample-echo-server"]
    }
  }
}
```

On `dotnet clean`, Imprint reads this manifest and removes exactly the files it installed — across all agents, across all packages. No leftover files, no accidental deletions.

### Package Authors Get It for Free

The multi-agent support is entirely in `Zakira.Imprint.Sdk`. Package authors do not need to change their `.targets` files, their skill content, or their MCP fragments. The same ~25-line `.targets` file that worked with v1.0.0 now automatically distributes to every agent the consumer has configured. All the package author needs to do is update their SDK reference to 1.1.0.

## Summary

Distributing AI Skills via NuGet is a natural extension of how we already distribute tools like Roslyn Analyzers. With MCP Server Injection and multi-agent support, a single NuGet package can now deliver both static knowledge and dynamic tool configurations to every AI assistant your team uses. It solves real problems:

- No more copying files between projects
- Easy updates through package versioning
- Library authors can teach AI about their libraries
- MCP servers are configured automatically — no manual `mcp.json` editing
- No code changes or repository bloat
- One package, every AI agent — Copilot, Claude, Cursor, and more

The pattern is simple, it builds on existing NuGet and MSBuild infrastructure, and it just works.

If you maintain an internal library, consider adding AI skills to help users get started. If you have organization-wide standards, package them up. The barrier to entry is low, and the benefits compound as more people adopt the pattern.

You can find the complete source code and examples on [GitHub](https://github.com/MoaidHathot/SkillsViaNuget).

---

*Have questions or ideas for improvement? I would love to hear them. Find me on [Twitter](https://twitter.com/MoaidHathot) or [GitHub](https://github.com/moaidhathot).*
