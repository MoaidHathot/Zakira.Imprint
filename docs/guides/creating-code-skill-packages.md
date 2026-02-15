---
layout: default
title: Creating Code+Skills Packages
parent: Guides
nav_order: 2
description: "Ship libraries with AI skills that teach how to use them."
permalink: /guides/creating-code-skill-packages
---

# Creating Code+Skills Packages
{: .fs-9 }

Ship a library with AI skills that teach developers how to use it.
{: .fs-6 .fw-300 }

---

## Overview

Code+Skills packages combine:
- **A compiled library** (DLL) that consumers use at runtime
- **AI skills** that teach AI assistants how to use the library correctly

This is ideal for SDK authors, framework maintainers, and anyone shipping reusable code.

---

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- A library you want to enhance with AI skills

---

## Step 1: Create or Modify Your Library Project

If starting fresh:

```bash
mkdir StringUtils
cd StringUtils
dotnet new classlib -n StringUtils
cd StringUtils
```

---

## Step 2: Configure the Project File

Modify `StringUtils.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    
    <!-- Generate XML documentation -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    
    <!-- Package metadata -->
    <PackageId>MyOrg.StringUtils</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>String utility extensions with AI-powered usage guidance</Description>
    <PackageTags>strings;utilities;extensions;ai-skills</PackageTags>
    
    <!-- NOTE: Do NOT set these for code packages:
         - IncludeBuildOutput=false (we want the DLL)
         - DevelopmentDependency=true (consumers need runtime code)
    -->
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference the Imprint SDK -->
    <PackageReference Include="Zakira.Imprint.Sdk" Version="1.1.0">
      <PrivateAssets>compile</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <!-- Include skills -->
    <Imprint Include="skills\**\*" />
    
    <!-- Optionally include MCP servers -->
    <Imprint Include="mcp\*.mcp.json" Type="Mcp" />
  </ItemGroup>
</Project>
```

### Key Differences from Skills-Only

| Setting | Skills-Only | Code+Skills |
|:--------|:------------|:------------|
| `IncludeBuildOutput` | `false` | Not set (true) |
| `DevelopmentDependency` | `true` | Not set |
| `TargetFramework` | `netstandard2.0` | Actual target (`net8.0`) |

---

## Step 3: Implement Your Library

Create `StringExtensions.cs`:

```csharp
namespace StringUtils;

/// <summary>
/// Extension methods for string manipulation.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Converts a string to a URL-friendly slug.
    /// </summary>
    /// <param name="text">The text to slugify.</param>
    /// <returns>A lowercase, hyphenated slug.</returns>
    public static string Slugify(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Convert to lowercase
        var slug = text.ToLowerInvariant();
        
        // Replace spaces with hyphens
        slug = slug.Replace(' ', '-');
        
        // Remove non-alphanumeric characters (except hyphens)
        slug = new string(slug
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray());
        
        // Remove consecutive hyphens
        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");
        
        // Trim hyphens from ends
        return slug.Trim('-');
    }

    /// <summary>
    /// Truncates a string to the specified length with an ellipsis.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxLength">Maximum length including ellipsis.</param>
    /// <param name="ellipsis">The ellipsis to use (default "...").</param>
    /// <returns>The truncated string.</returns>
    public static string Truncate(this string text, int maxLength, string ellipsis = "...")
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        var truncateLength = maxLength - ellipsis.Length;
        if (truncateLength <= 0)
            return ellipsis[..maxLength];

        return text[..truncateLength] + ellipsis;
    }

    /// <summary>
    /// Masks a portion of the string with a specified character.
    /// </summary>
    /// <param name="text">The text to mask.</param>
    /// <param name="visibleStart">Number of characters to show at start.</param>
    /// <param name="visibleEnd">Number of characters to show at end.</param>
    /// <param name="maskChar">The masking character (default '*').</param>
    /// <returns>The masked string.</returns>
    public static string Mask(this string text, int visibleStart = 4, int visibleEnd = 4, char maskChar = '*')
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (text.Length <= visibleStart + visibleEnd)
            return new string(maskChar, text.Length);

        var start = text[..visibleStart];
        var end = text[^visibleEnd..];
        var maskLength = text.Length - visibleStart - visibleEnd;

        return start + new string(maskChar, maskLength) + end;
    }

    /// <summary>
    /// Converts a string to Title Case.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>The text in Title Case.</returns>
    public static string ToTitleCase(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var cultureInfo = System.Globalization.CultureInfo.CurrentCulture;
        var textInfo = cultureInfo.TextInfo;
        return textInfo.ToTitleCase(text.ToLower());
    }
}
```

---

## Step 4: Create Skills Directory

```bash
mkdir skills
mkdir skills/string-utils
```

---

## Step 5: Write Skills That Teach Your Library

Create `skills/string-utils/SKILL.md`:

```markdown
# StringUtils Library

This project uses the `StringUtils` library for string manipulation. Use these extensions for consistent, tested string operations.

## Available Extensions

### Slugify()

Converts text to URL-friendly slugs:

```csharp
using StringUtils;

var title = "Hello World! This is a Test";
var slug = title.Slugify();
// Result: "hello-world-this-is-a-test"
```

**Use Cases:**
- URL generation
- File naming
- Database identifiers

### Truncate(maxLength, ellipsis)

Truncates text with an ellipsis:

```csharp
using StringUtils;

var longText = "This is a very long piece of text that needs truncation";
var preview = longText.Truncate(30);
// Result: "This is a very long piece o..."

// Custom ellipsis
var preview2 = longText.Truncate(30, "…");
// Result: "This is a very long piece of…"
```

**Use Cases:**
- Preview text in lists
- Tooltip content
- Meta descriptions

### Mask(visibleStart, visibleEnd, maskChar)

Masks sensitive data:

```csharp
using StringUtils;

var creditCard = "4111111111111111";
var masked = creditCard.Mask(4, 4);
// Result: "4111********1111"

var email = "user@example.com";
var maskedEmail = email.Mask(2, 4);
// Result: "us**********m.com"
```

**Use Cases:**
- Displaying credit card numbers
- Masking API keys in logs
- Privacy-compliant data display

### ToTitleCase()

Converts to Title Case:

```csharp
using StringUtils;

var input = "the quick BROWN fox";
var title = input.ToTitleCase();
// Result: "The Quick Brown Fox"
```

**Use Cases:**
- Formatting names
- Headlines
- Display labels

## Best Practices

1. **Always use StringUtils extensions** instead of manual string manipulation
2. **Use Slugify()** for any URL generation - don't manually replace characters
3. **Use Mask()** when displaying sensitive data - never show full credit cards or SSNs
4. **Use Truncate()** for preview text - it handles edge cases correctly

## Anti-Patterns to Avoid

```csharp
// DON'T: Manual slug creation
var slug = title.ToLower().Replace(" ", "-"); // Missing edge cases!

// DO: Use Slugify()
var slug = title.Slugify();

// DON'T: Manual truncation
var preview = text.Length > 30 ? text.Substring(0, 27) + "..." : text;

// DO: Use Truncate()
var preview = text.Truncate(30);
```

## Adding the Package

If not already added:

```bash
dotnet add package MyOrg.StringUtils
```

## Namespace

```csharp
using StringUtils;
```

All extensions are available on any `string` instance after importing the namespace.
```

---

## Step 6: Build and Test

```bash
dotnet build
dotnet pack -c Release -o ./packages
```

---

## Step 7: Test the Package

Create a test consumer project:

```bash
cd ..
mkdir TestConsumer
cd TestConsumer
dotnet new console

# Local package source
cat > nuget.config << 'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local" value="../StringUtils/packages" />
  </packageSources>
</configuration>
EOF

# Install the package
dotnet add package MyOrg.StringUtils --version 1.0.0
```

Update `Program.cs`:

```csharp
using StringUtils;

var title = "Hello World! This is Amazing";
Console.WriteLine($"Slug: {title.Slugify()}");
Console.WriteLine($"Preview: {title.Truncate(20)}");
```

Build and run:

```bash
mkdir .github  # Enable Copilot detection
dotnet build
dotnet run
```

Verify skills were installed:

```bash
cat .github/skills/string-utils/SKILL.md
```

---

## Step 8: Include MCP Server (Optional)

If your library has an associated MCP server, include it:

Create `mcp/` directory:

```bash
mkdir mcp
```

Create `mcp/StringUtils.mcp.json`:

```json
{
  "servers": {
    "string-utils-server": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@myorg/string-utils-mcp-server"],
      "env": {
        "STRING_UTILS_VERSION": "1.0.0"
      }
    }
  }
}
```

Update your `.csproj`:

```xml
<ItemGroup>
  <Imprint Include="skills\**\*" />
  <Imprint Include="mcp\*.mcp.json" Type="Mcp" />
</ItemGroup>
```

---

## Package Structure

The final package structure:

```
MyOrg.StringUtils.1.0.0.nupkg
├── lib/
│   └── net8.0/
│       ├── StringUtils.dll
│       └── StringUtils.xml
├── build/
│   └── StringUtils.targets
├── skills/
│   └── string-utils/
│       └── SKILL.md
└── mcp/
    └── StringUtils.mcp.json
```

---

## Best Practices

### Skill Content Should Reference Your APIs

```markdown
## Instead of Manual Implementation

```csharp
// Don't do this
var result = text.ToLower().Replace(" ", "-");

// Use our library
var result = text.Slugify();
```
```

### Include Common Use Cases

Show how your library solves real problems:

```markdown
## Generating Blog Post URLs

```csharp
var post = new BlogPost 
{ 
    Title = "10 Tips for Better Code"
};
post.Slug = post.Title.Slugify();
// Result: "10-tips-for-better-code"
```
```

### Document Edge Cases

```markdown
## Edge Cases

`Slugify()` handles:
- Multiple spaces: "Hello    World" → "hello-world"
- Special characters: "Hello! World?" → "hello-world"
- Unicode: "Café Münster" → "cafe-munster"
- Empty strings: returns empty string
```

---

## Next Steps

- [MCP Server Configuration](/guides/mcp-server-configuration) - Add MCP servers
- [Publishing Packages](/guides/publishing-packages) - Publish to NuGet.org
- [Configuration Reference](/reference/configuration) - All options
