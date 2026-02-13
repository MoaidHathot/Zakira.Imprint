using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Zakira.Imprint.Sample.WithCode
{
    /// <summary>
    /// Extension methods for <see cref="string"/> providing common text transformations.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Converts a string to a URL-friendly slug.
        /// Lowercases, replaces spaces/special chars with hyphens, removes non-alphanumeric characters,
        /// and collapses consecutive hyphens.
        /// </summary>
        /// <example>
        /// "Hello World! This is C#".Slugify() => "hello-world-this-is-c"
        /// </example>
        public static string Slugify(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            // Remove diacritics first
            value = value.RemoveDiacritics();

            // Lowercase
            value = value.ToLowerInvariant();

            // Replace spaces and common separators with hyphens
            value = Regex.Replace(value, @"[\s_]+", "-");

            // Remove non-alphanumeric characters (except hyphens)
            value = Regex.Replace(value, @"[^a-z0-9\-]", "");

            // Collapse consecutive hyphens
            value = Regex.Replace(value, @"-{2,}", "-");

            // Trim leading/trailing hyphens
            return value.Trim('-');
        }

        /// <summary>
        /// Truncates a string to the specified maximum length, appending an ellipsis suffix if truncated.
        /// </summary>
        /// <param name="value">The string to truncate.</param>
        /// <param name="maxLength">Maximum length of the result including the suffix.</param>
        /// <param name="suffix">The suffix to append when truncated. Defaults to "...".</param>
        /// <example>
        /// "Hello World".Truncate(8) => "Hello..."
        /// "Hi".Truncate(10) => "Hi"
        /// </example>
        public static string Truncate(this string value, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            if (maxLength < 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be non-negative.");

            if (suffix == null)
                suffix = string.Empty;

            if (value.Length <= maxLength)
                return value;

            if (maxLength <= suffix.Length)
                return suffix.Substring(0, maxLength);

            return value.Substring(0, maxLength - suffix.Length) + suffix;
        }

        /// <summary>
        /// Masks a portion of the string, useful for obscuring sensitive data like emails or credit cards.
        /// </summary>
        /// <param name="value">The string to mask.</param>
        /// <param name="visibleStart">Number of characters to keep visible at the start.</param>
        /// <param name="visibleEnd">Number of characters to keep visible at the end.</param>
        /// <param name="maskChar">The character used for masking. Defaults to '*'.</param>
        /// <example>
        /// "1234567890".Mask(2, 2) => "12******90"
        /// "user@email.com".Mask(3, 4) => "use*******l.com" -- use MaskEmail for smarter email masking
        /// </example>
        public static string Mask(this string value, int visibleStart = 2, int visibleEnd = 2, char maskChar = '*')
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            if (visibleStart < 0)
                throw new ArgumentOutOfRangeException(nameof(visibleStart));
            if (visibleEnd < 0)
                throw new ArgumentOutOfRangeException(nameof(visibleEnd));

            if (visibleStart + visibleEnd >= value.Length)
                return value;

            var masked = new StringBuilder(value.Length);
            masked.Append(value, 0, visibleStart);
            masked.Append(maskChar, value.Length - visibleStart - visibleEnd);
            masked.Append(value, value.Length - visibleEnd, visibleEnd);
            return masked.ToString();
        }

        /// <summary>
        /// Converts a string to Title Case (each word's first letter capitalized).
        /// </summary>
        /// <example>
        /// "hello world example".ToTitleCase() => "Hello World Example"
        /// </example>
        public static string ToTitleCase(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value ?? string.Empty;

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        }

        /// <summary>
        /// Removes diacritics (accent marks) from a string, replacing accented characters with their
        /// ASCII equivalents.
        /// </summary>
        /// <example>
        /// "café résumé".RemoveDiacritics() => "cafe resume"
        /// </example>
        public static string RemoveDiacritics(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Converts a string to camelCase.
        /// </summary>
        /// <example>
        /// "hello world example".ToCamelCase() => "helloWorldExample"
        /// "some-kebab-case".ToCamelCase() => "someKebabCase"
        /// </example>
        public static string ToCamelCase(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value ?? string.Empty;

            var words = Regex.Split(value, @"[\s_\-]+")
                .Where(w => w.Length > 0)
                .ToArray();

            if (words.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.Append(words[0].ToLowerInvariant());

            for (int i = 1; i < words.Length; i++)
            {
                sb.Append(char.ToUpperInvariant(words[i][0]));
                if (words[i].Length > 1)
                    sb.Append(words[i].Substring(1).ToLowerInvariant());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts a string to snake_case.
        /// </summary>
        /// <example>
        /// "Hello World".ToSnakeCase() => "hello_world"
        /// "camelCaseExample".ToSnakeCase() => "camel_case_example"
        /// </example>
        public static string ToSnakeCase(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value ?? string.Empty;

            // Insert underscore before uppercase letters that follow lowercase letters
            var result = Regex.Replace(value, @"([a-z])([A-Z])", "$1_$2");
            // Replace spaces, hyphens with underscores
            result = Regex.Replace(result, @"[\s\-]+", "_");
            // Remove non-alphanumeric chars except underscore
            result = Regex.Replace(result, @"[^a-zA-Z0-9_]", "");
            // Collapse multiple underscores
            result = Regex.Replace(result, @"_{2,}", "_");

            return result.Trim('_').ToLowerInvariant();
        }

        /// <summary>
        /// Reverses the characters in a string.
        /// </summary>
        /// <example>
        /// "Hello".Reverse() => "olleH"
        /// </example>
        public static string Reverse(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            var chars = value.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        /// <summary>
        /// Counts the number of words in a string.
        /// </summary>
        /// <example>
        /// "Hello world, this is a test".WordCount() => 6
        /// </example>
        public static int WordCount(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            return Regex.Split(value.Trim(), @"\s+").Length;
        }
    }
}
