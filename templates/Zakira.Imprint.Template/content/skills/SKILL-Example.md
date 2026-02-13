# Example Skill

This is an example skill file. Replace this with your actual AI skill content.

## About Skills

AI Skills are markdown files that provide context and guidance to AI assistants like GitHub Copilot. They help the AI understand:

- Your coding standards and conventions
- Domain-specific knowledge
- Architectural patterns you prefer
- Common patterns and anti-patterns in your codebase

## Writing Effective Skills

### Structure

A good skill file includes:

1. **Clear title** - What domain or topic does this skill cover?
2. **Context** - Background information the AI needs
3. **DO/DON'T sections** - Explicit guidance on preferred patterns
4. **Code examples** - Show, don't just tell

### Example Pattern

```markdown
# [Topic] Best Practices

## Overview
Brief description of what this skill covers.

## DO: Preferred Pattern
```code
// Good example
```

## DON'T: Anti-Pattern
```code
// Bad example - explain why
```

## Configuration
Any relevant configuration or setup instructions.
```

## Next Steps

1. Rename this file to match your skill (e.g., `SKILL-CodingStandards.md`)
2. Replace the content with your actual skill
3. Add more skill files as needed
4. Pack and publish: `dotnet pack -o ./packages`
