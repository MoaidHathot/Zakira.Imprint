---
layout: default
title: Contributing
nav_order: 6
---

# Contributing to Zakira.Imprint

Thank you for your interest in contributing to Zakira.Imprint! This guide will help you get started.

---

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for everyone.

---

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
- Git
- A code editor (VS Code, Visual Studio, Rider, etc.)

### Setting Up the Development Environment

1. **Fork and clone the repository:**
   ```bash
   git clone https://github.com/YOUR_USERNAME/Zakira.Imprint.git
   cd Zakira.Imprint
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Build the solution:**
   ```bash
   dotnet build
   ```

4. **Run tests:**
   ```bash
   dotnet test
   ```

---

## Project Structure

```
Zakira.Imprint/
├── src/
│   └── Zakira.Imprint.Sdk/       # Main SDK project
│       ├── build/                 # MSBuild props/targets
│       ├── AgentConfig.cs         # Agent configuration definitions
│       ├── ImprintGenerateTargets.cs
│       ├── ImprintCopyContent.cs
│       ├── ImprintMergeMcpServers.cs
│       ├── ImprintCleanContent.cs
│       └── ImprintCleanMcpServers.cs
├── tests/                         # Test projects
├── samples/                       # Sample packages
├── docs/                          # Documentation (Jekyll site)
└── Zakira.Imprint.sln
```

---

## Development Workflow

### Making Changes

1. **Create a feature branch:**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** following the coding guidelines below

3. **Write or update tests** for your changes

4. **Build and test:**
   ```bash
   dotnet build
   dotnet test
   ```

5. **Commit with a clear message:**
   ```bash
   git commit -m "feat: add support for new agent type"
   ```

### Commit Message Format

We follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `refactor:` - Code refactoring
- `test:` - Adding or updating tests
- `chore:` - Maintenance tasks

Examples:
```
feat: add support for Windsurf agent
fix: correct MCP server cleanup on package removal
docs: add troubleshooting guide for manifest conflicts
refactor: simplify target generation logic
```

---

## Coding Guidelines

### C# Style

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful, descriptive names
- Keep methods focused and small
- Add XML documentation for public APIs

### MSBuild Style

- Use descriptive property and item names
- Comment complex logic
- Follow existing patterns in the codebase

### Example Code Style

```csharp
/// <summary>
/// Generates the .targets file for a skill package.
/// </summary>
/// <param name="skillName">The name of the skill.</param>
/// <param name="items">The imprint items to include.</param>
/// <returns>The generated targets content.</returns>
public string GenerateTargets(string skillName, IEnumerable<ImprintItem> items)
{
    ArgumentNullException.ThrowIfNull(skillName);
    ArgumentNullException.ThrowIfNull(items);

    var builder = new StringBuilder();
    
    foreach (var item in items)
    {
        AppendItemTarget(builder, item);
    }

    return builder.ToString();
}
```

---

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/Zakira.Imprint.Sdk.Tests/
```

### Writing Tests

- Place tests in the corresponding test project
- Use descriptive test names that explain the scenario
- Follow the Arrange-Act-Assert pattern

```csharp
[Fact]
public void GenerateTargets_WithValidSkillName_ReturnsCorrectContent()
{
    // Arrange
    var generator = new TargetsGenerator();
    var skillName = "TestSkill";

    // Act
    var result = generator.GenerateTargets(skillName, items);

    // Assert
    Assert.Contains(skillName, result);
}
```

---

## Documentation

### Building Documentation Locally

```bash
cd docs
bundle install
bundle exec jekyll serve
```

Visit `http://localhost:4000` to preview.

### Documentation Guidelines

- Use clear, concise language
- Include code examples where appropriate
- Update the navigation order if adding new pages
- Test all code examples before committing

---

## Pull Request Process

### Before Submitting

1. Ensure all tests pass
2. Update documentation if needed
3. Add entries to CHANGELOG.md if applicable
4. Rebase on the latest main branch

### Submitting a PR

1. **Push your branch:**
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Create a Pull Request** on GitHub

3. **Fill out the PR template** with:
   - Description of changes
   - Related issue numbers
   - Testing performed
   - Breaking changes (if any)

### PR Review Process

- Maintainers will review your PR
- Address any feedback promptly
- Once approved, a maintainer will merge

---

## Adding New Features

### Adding a New Agent

1. **Update `AgentConfig.cs`:**
   ```csharp
   public static readonly AgentConfig NewAgent = new()
   {
       Name = "newagent",
       SkillPath = ".newagent/skills/{0}",
       SkillFileName = "SKILL.md",
       McpConfigPath = ".newagent/mcp.json",
       McpRootKey = "mcpServers"
   };
   ```

2. **Add to `AllAgents` collection**

3. **Update MSBuild props** for default inclusion

4. **Add tests** for the new agent

5. **Update documentation**

### Adding New MSBuild Properties

1. **Define in `Zakira.Imprint.Sdk.props`:**
   ```xml
   <PropertyGroup>
     <NewProperty Condition="'$(NewProperty)' == ''">default</NewProperty>
   </PropertyGroup>
   ```

2. **Use in tasks** as needed

3. **Document** in the Configuration reference

4. **Add tests** for the new property

---

## Reporting Issues

### Bug Reports

Include:
- SDK version
- .NET SDK version
- Steps to reproduce
- Expected vs actual behavior
- Relevant project configuration

### Feature Requests

Include:
- Use case description
- Proposed solution (if any)
- Alternatives considered

---

## Release Process

Releases are managed by maintainers:

1. Update version in `Directory.Build.props`
2. Update CHANGELOG.md
3. Create a release tag
4. CI/CD publishes to NuGet

---

## Questions?

- Open a [GitHub Discussion](https://github.com/zakira/imprint/discussions)
- Check existing issues and discussions first
- Be specific and provide context

---

Thank you for contributing to Zakira.Imprint!
