# Zakira.Imprint.Sample.WithCode

A sample NuGet package demonstrating how to ship **both a compiled library and AI skills** using the Imprint pattern.

## What This Package Contains

1. **A .NET library** (`Zakira.Imprint.Sample.WithCode.dll`) with string utility classes:
   - `StringExtensions` - Extension methods: `Slugify()`, `Truncate()`, `Mask()`, `ToTitleCase()`, `RemoveDiacritics()`, `ToCamelCase()`, `ToSnakeCase()`, `Reverse()`, `WordCount()`
   - `StringHelper` - Static helpers: `MaskEmail()`, `MaskCreditCard()`, `ShortHash()`, `GenerateRandom()`, `IsValidEmail()`, `GetInitials()`

2. **AI skill files** (copied to `.github/skills/` on build) that teach AI assistants how to use the library API

3. **MCP server configuration** (merged into `.vscode/mcp.json` on build)

## Installation

```
dotnet add package Zakira.Imprint.Sample.WithCode
```

## Usage

```csharp
using Zakira.Imprint.Sample.WithCode;

// Extension methods
var slug = "Hello World!".Slugify();           // "hello-world"
var truncated = "Long text here".Truncate(10); // "Long te..."
var masked = "1234567890".Mask(2, 2);          // "12******90"

// Static helpers
var email = StringHelper.MaskEmail("user@example.com");    // "us**@example.com"
var hash = StringHelper.ShortHash("my-input");             // "a1b2c3d4"
var initials = StringHelper.GetInitials("John Doe");       // "JD"
```

## How It Works

This package uses the **Zakira.Imprint.Sdk** to automatically copy AI skill files and merge MCP configuration into consumer projects at build time. Unlike skill-only packages, this package also ships a compiled DLL that provides actual runtime functionality.

When you install this package and build your project:
- The `Zakira.Imprint.Sample.WithCode.dll` is referenced like any normal NuGet library
- Skill markdown files are copied to `.github/skills/`
- MCP server fragments are merged into `.vscode/mcp.json`

On `dotnet clean`, the skill files and MCP entries are removed.
