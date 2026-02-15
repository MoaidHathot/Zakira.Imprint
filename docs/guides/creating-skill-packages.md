---
layout: default
title: Creating Skill Packages
parent: Guides
nav_order: 1
description: "Step-by-step guide to creating skills-only packages."
permalink: /guides/creating-skill-packages
---

# Creating Skill Packages
{: .fs-9 }

Build and publish NuGet packages that distribute AI skills.
{: .fs-6 .fw-300 }

---

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- A NuGet account (for publishing)

---

## Step 1: Create the Project

Create a new directory and class library project:

```bash
mkdir MyOrg.Skills.Security
cd MyOrg.Skills.Security
dotnet new classlib
```

---

## Step 2: Configure the Project File

Replace the contents of `MyOrg.Skills.Security.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Target netstandard2.0 for widest compatibility -->
    <TargetFramework>netstandard2.0</TargetFramework>
    
    <!-- Package metadata -->
    <PackageId>MyOrg.Skills.Security</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Company>Your Organization</Company>
    <Description>Security best practices skills for AI assistants</Description>
    <PackageTags>ai;skills;security;copilot;claude;cursor</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/your-org/skills-security</PackageProjectUrl>
    <RepositoryUrl>https://github.com/your-org/skills-security</RepositoryUrl>
    
    <!-- Skills-only package settings -->
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
    <NoPackageAnalysis>true</NoPackageAnalysis>
    <SuppressDependenciesWhenPacking>false</SuppressDependenciesWhenPacking>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference the Imprint SDK -->
    <PackageReference Include="Zakira.Imprint.Sdk" Version="1.1.0">
      <PrivateAssets>compile</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <!-- Include all skill files -->
    <Imprint Include="skills\**\*" />
  </ItemGroup>
</Project>
```

### Key Settings Explained

| Setting | Value | Purpose |
|:--------|:------|:--------|
| `TargetFramework` | `netstandard2.0` | Maximum compatibility |
| `IncludeBuildOutput` | `false` | Don't include a DLL |
| `DevelopmentDependency` | `true` | Build-time only |
| `SuppressDependenciesWhenPacking` | `false` | Include SDK dependency |
| `PrivateAssets` | `compile` | SDK targets flow to consumers |

---

## Step 3: Delete Unnecessary Files

Remove the default class file:

```bash
rm Class1.cs
```

---

## Step 4: Create the Skills Directory

```bash
mkdir skills
mkdir skills/authentication
mkdir skills/input-validation
mkdir skills/encryption
```

---

## Step 5: Write Your Skills

### skills/authentication/SKILL.md

```markdown
# Authentication Best Practices

## Password Handling

- Never store passwords in plain text
- Use bcrypt, scrypt, or Argon2 for password hashing
- Implement account lockout after failed attempts
- Use secure password reset flows

## Session Management

- Generate cryptographically random session IDs
- Implement session timeouts
- Invalidate sessions on logout
- Use HttpOnly and Secure flags for cookies

## Multi-Factor Authentication

- Implement TOTP (Time-based One-Time Passwords)
- Support hardware security keys (WebAuthn/FIDO2)
- Provide backup codes for account recovery

## Code Examples

```csharp
// Good: Using proper password hashing
var hashedPassword = BCrypt.HashPassword(password, BCrypt.GenerateSalt(12));

// Good: Verifying passwords
if (BCrypt.Verify(inputPassword, storedHash))
{
    // Password is correct
}
```

## Anti-Patterns to Avoid

- Storing passwords with MD5 or SHA1
- Rolling your own authentication
- Hardcoding credentials
- Transmitting passwords over non-HTTPS connections
```

### skills/input-validation/SKILL.md

```markdown
# Input Validation Best Practices

## General Principles

- Validate all input on the server side
- Never trust client-side validation alone
- Use allowlists over denylists when possible
- Validate type, length, format, and range

## SQL Injection Prevention

Always use parameterized queries:

```csharp
// GOOD: Parameterized query
using var cmd = new SqlCommand(
    "SELECT * FROM Users WHERE Id = @Id", 
    connection);
cmd.Parameters.AddWithValue("@Id", userId);

// BAD: String concatenation
var query = $"SELECT * FROM Users WHERE Id = {userId}"; // NEVER DO THIS
```

## XSS Prevention

- Encode output based on context (HTML, JavaScript, URL)
- Use Content Security Policy headers
- Validate and sanitize HTML input

```csharp
// Good: HTML encoding output
var safeOutput = HttpUtility.HtmlEncode(userInput);
```

## File Upload Validation

- Validate file type by content, not just extension
- Limit file size
- Store uploads outside the web root
- Generate random filenames
```

### skills/encryption/SKILL.md

```markdown
# Encryption Best Practices

## Symmetric Encryption

- Use AES-256-GCM for authenticated encryption
- Never reuse IVs/nonces
- Use cryptographically secure random number generators

```csharp
// Good: AES-GCM encryption
using var aes = new AesGcm(key);
var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
RandomNumberGenerator.Fill(nonce);
aes.Encrypt(nonce, plaintext, ciphertext, tag);
```

## Key Management

- Never hardcode encryption keys
- Use a key management service (Azure Key Vault, AWS KMS)
- Rotate keys regularly
- Use different keys for different purposes

## Data at Rest

- Encrypt sensitive database columns
- Use transparent data encryption (TDE) where available
- Encrypt backups

## Data in Transit

- Use TLS 1.2 or higher
- Enable HSTS
- Use certificate pinning for mobile apps
```

---

## Step 6: Build and Verify

Build the project to verify everything works:

```bash
dotnet build
```

Check the build output for Imprint messages.

---

## Step 7: Create the Package

```bash
dotnet pack -c Release -o ./packages
```

This creates `MyOrg.Skills.Security.1.0.0.nupkg` in the `packages` folder.

---

## Step 8: Inspect the Package

Verify the package contents:

```bash
# List package contents (requires unzip)
unzip -l packages/MyOrg.Skills.Security.1.0.0.nupkg
```

You should see:

```
Archive:  MyOrg.Skills.Security.1.0.0.nupkg
  Length      Date    Time    Name
---------  ---------- -----   ----
      xxx  xx-xx-xxxx xx:xx   build/MyOrg.Skills.Security.targets
      xxx  xx-xx-xxxx xx:xx   skills/authentication/SKILL.md
      xxx  xx-xx-xxxx xx:xx   skills/input-validation/SKILL.md
      xxx  xx-xx-xxxx xx:xx   skills/encryption/SKILL.md
      xxx  xx-xx-xxxx xx:xx   MyOrg.Skills.Security.nuspec
---------                     -------
```

---

## Step 9: Test Locally

Create a test project:

```bash
cd ..
mkdir TestConsumer
cd TestConsumer
dotnet new console

# Configure local package source
cat > nuget.config << 'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local" value="../MyOrg.Skills.Security/packages" />
  </packageSources>
</configuration>
EOF

# Install the package
dotnet add package MyOrg.Skills.Security --version 1.0.0

# Create agent directories
mkdir .github
mkdir .claude

# Build and verify
dotnet build
```

Check that skills were installed:

```bash
ls .github/skills/
ls .claude/skills/
```

---

## Step 10: Publish to NuGet

When ready to publish:

```bash
# Set your API key (one-time setup)
dotnet nuget push packages/MyOrg.Skills.Security.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

---

## Best Practices

### Skill Organization

- Group related skills in subdirectories
- Use clear, descriptive directory names
- Keep individual skill files focused

```
skills/
├── authentication/
│   └── SKILL.md
├── authorization/
│   └── SKILL.md
├── input-validation/
│   └── SKILL.md
└── encryption/
    └── SKILL.md
```

### Skill Content

- Start with a clear title and purpose
- Include both "do" and "don't" examples
- Provide code snippets where applicable
- Reference official documentation

### Versioning

- Follow semantic versioning
- Document changes in release notes
- Consider backward compatibility

---

## Complete Example Project

```
MyOrg.Skills.Security/
├── MyOrg.Skills.Security.csproj
├── skills/
│   ├── authentication/
│   │   └── SKILL.md
│   ├── input-validation/
│   │   └── SKILL.md
│   └── encryption/
│       └── SKILL.md
└── packages/
    └── MyOrg.Skills.Security.1.0.0.nupkg
```

---

## Next Steps

- [Adding MCP Servers](/guides/mcp-server-configuration) - Include MCP configurations
- [Publishing Packages](/guides/publishing-packages) - Publish to NuGet.org
- [Configuration Reference](/reference/configuration) - All configuration options
