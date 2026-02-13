---
name: Zakira.Imprint.Sample.WithCode - String Utilities Library
description: Comprehensive guide for AI assistants on using the StringExtensions and StringHelper APIs
version: 1.0.0
author: Imprint
---

# Zakira.Imprint.Sample.WithCode - String Utilities

This skill teaches you how to use the `Zakira.Imprint.Sample.WithCode` NuGet package, which provides string manipulation utilities via the `StringExtensions` and `StringHelper` classes.

## Namespace

All types are in the `Zakira.Imprint.Sample.WithCode` namespace:

```csharp
using Zakira.Imprint.Sample.WithCode;
```

## StringExtensions (Extension Methods)

These are extension methods on `string`. After adding the `using` directive, call them directly on any string instance.

### Slugify

Converts a string to a URL-friendly slug (lowercase, hyphens, no special chars).

```csharp
"Hello World! This is C#".Slugify()    // => "hello-world-this-is-c"
"Café Résumé".Slugify()                // => "cafe-resume" (diacritics removed)
"  Multiple   Spaces  ".Slugify()      // => "multiple-spaces"
```

### Truncate

Truncates to a maximum length with a configurable suffix.

```csharp
"Hello World".Truncate(8)              // => "Hello..."
"Hello World".Truncate(8, "~")         // => "Hello W~"
"Hi".Truncate(10)                      // => "Hi" (no truncation needed)
```

### Mask

Masks the middle portion of a string, keeping visible characters at start and end.

```csharp
"1234567890".Mask(2, 2)                // => "12******90"
"SensitiveData".Mask(3, 3)             // => "Sen*******ata"
"1234567890".Mask(2, 2, '#')           // => "12######90"
```

### ToTitleCase

Converts to Title Case (first letter of each word capitalized).

```csharp
"hello world example".ToTitleCase()    // => "Hello World Example"
"ALL CAPS INPUT".ToTitleCase()         // => "All Caps Input"
```

### RemoveDiacritics

Strips accent marks from characters.

```csharp
"café résumé".RemoveDiacritics()       // => "cafe resume"
"naïve über".RemoveDiacritics()        // => "naive uber"
```

### ToCamelCase

Converts to camelCase from any word-separated format.

```csharp
"hello world example".ToCamelCase()    // => "helloWorldExample"
"some-kebab-case".ToCamelCase()        // => "someKebabCase"
"SOME_SCREAMING_SNAKE".ToCamelCase()   // => "someScreamingSnake"
```

### ToSnakeCase

Converts to snake_case.

```csharp
"Hello World".ToSnakeCase()            // => "hello_world"
"camelCaseExample".ToSnakeCase()       // => "camel_case_example"
"PascalCaseInput".ToSnakeCase()        // => "pascal_case_input"
```

### Reverse

Reverses the characters in a string.

```csharp
"Hello".Reverse()                      // => "olleH"
"12345".Reverse()                      // => "54321"
```

### WordCount

Counts the number of whitespace-separated words.

```csharp
"Hello world, this is a test".WordCount()  // => 6
"  ".WordCount()                           // => 0
```

## StringHelper (Static Methods)

These are static methods. Call them via `StringHelper.MethodName(...)`.

### MaskEmail

Smart email masking that preserves the domain.

```csharp
StringHelper.MaskEmail("john.doe@example.com")     // => "jo******@example.com"
StringHelper.MaskEmail("ab@test.com")               // => "ab@test.com" (too short to mask)
StringHelper.MaskEmail("admin@corp.com", 3)         // => "adm**@corp.com"
```

### MaskCreditCard

Masks all but the last 4 digits of a credit card number, re-formatted with hyphens.

```csharp
StringHelper.MaskCreditCard("4111-1111-1111-1111")  // => "****-****-****-1111"
StringHelper.MaskCreditCard("4111111111111111")      // => "****-****-****-1111"
```

### ShortHash

Generates a deterministic short hash (MD5-based hex) for a string input.

```csharp
StringHelper.ShortHash("hello world")       // => "5eb63bbb" (8 chars, default)
StringHelper.ShortHash("hello world", 12)   // => "5eb63bbbe01e" (12 chars)
```

Use cases: cache keys, deduplication tokens, short identifiers.

### GenerateRandom

Generates a random alphanumeric string of the desired length.

```csharp
StringHelper.GenerateRandom(12)              // => e.g. "aB3xK9mP2qLw"
StringHelper.GenerateRandom(6, "0123456789") // => e.g. "384729" (digits only)
```

### IsValidEmail

Basic email format validation.

```csharp
StringHelper.IsValidEmail("user@example.com")   // => true
StringHelper.IsValidEmail("not-an-email")        // => false
StringHelper.IsValidEmail("")                    // => false
```

### GetInitials

Extracts uppercase initials from a name.

```csharp
StringHelper.GetInitials("John Michael Doe")     // => "JMD"
StringHelper.GetInitials("Alice")                 // => "A"
StringHelper.GetInitials("A B C D E", 2)          // => "AB" (max 2 initials)
```

## Common Patterns

### Preparing user-generated content for URL slugs
```csharp
var title = "My Blog Post: A Café Story!";
var slug = title.Slugify();  // => "my-blog-post-a-cafe-story"
var url = $"/posts/{slug}";
```

### Displaying masked sensitive data in logs
```csharp
var email = "customer@company.com";
var card = "4111-1111-1111-1111";
Console.WriteLine($"Email: {StringHelper.MaskEmail(email)}");
Console.WriteLine($"Card: {StringHelper.MaskCreditCard(card)}");
```

### Converting between naming conventions
```csharp
var input = "myPropertyName";
var snake = input.ToSnakeCase();     // => "my_property_name"
var camel = snake.ToCamelCase();     // => "myPropertyName"
var slug = input.Slugify();          // => "my-property-name" (kebab-like)
```

## Error Handling

- All extension methods handle `null` and empty strings gracefully (return empty string or the input).
- `Truncate` throws `ArgumentOutOfRangeException` if `maxLength < 0`.
- `Mask` throws `ArgumentOutOfRangeException` if `visibleStart` or `visibleEnd` are negative.
- `ShortHash` throws `ArgumentNullException` if input is null, and `ArgumentOutOfRangeException` if length is out of range.

## Source Package

- **Package**: Zakira.Imprint.Sample.WithCode
- **Installed Via**: NuGet (provides both the DLL and these skill files)
- **Skill Location**: `.github/skills/`
