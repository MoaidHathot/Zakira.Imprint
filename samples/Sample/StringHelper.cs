using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Zakira.Imprint.Sample.WithCode
{
    /// <summary>
    /// Static helper methods for common string operations.
    /// </summary>
    public static class StringHelper
    {
        /// <summary>
        /// Masks an email address, showing only the first few characters of the local part
        /// and the domain.
        /// </summary>
        /// <param name="email">The email address to mask.</param>
        /// <param name="visibleChars">Number of characters to keep visible in the local part. Defaults to 2.</param>
        /// <param name="maskChar">The masking character. Defaults to '*'.</param>
        /// <returns>The masked email, or the original string if it's not a valid email format.</returns>
        /// <example>
        /// MaskEmail("john.doe@example.com") => "jo******@example.com"
        /// </example>
        public static string MaskEmail(string email, int visibleChars = 2, char maskChar = '*')
        {
            if (string.IsNullOrEmpty(email))
                return email ?? string.Empty;

            var atIndex = email.IndexOf('@');
            if (atIndex <= 0)
                return email;

            var localPart = email.Substring(0, atIndex);
            var domain = email.Substring(atIndex);

            if (localPart.Length <= visibleChars)
                return localPart + domain;

            return localPart.Substring(0, visibleChars)
                 + new string(maskChar, localPart.Length - visibleChars)
                 + domain;
        }

        /// <summary>
        /// Masks a credit card number, showing only the last 4 digits.
        /// Removes spaces/hyphens before masking, then re-formats in groups of 4.
        /// </summary>
        /// <example>
        /// MaskCreditCard("4111-1111-1111-1111") => "****-****-****-1111"
        /// </example>
        public static string MaskCreditCard(string cardNumber, char maskChar = '*')
        {
            if (string.IsNullOrEmpty(cardNumber))
                return cardNumber ?? string.Empty;

            // Strip non-digit characters
            var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
            if (digits.Length < 4)
                return new string(maskChar, digits.Length);

            var masked = new string(maskChar, digits.Length - 4) + digits.Substring(digits.Length - 4);

            // Re-format in groups of 4 with hyphens
            var sb = new StringBuilder();
            for (int i = 0; i < masked.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                    sb.Append('-');
                sb.Append(masked[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a deterministic short hash for a given input string.
        /// Useful for creating cache keys, deduplication tokens, or short identifiers.
        /// </summary>
        /// <param name="input">The input string to hash.</param>
        /// <param name="length">Desired length of the hash output (max 32). Defaults to 8.</param>
        /// <returns>A lowercase hex string of the specified length.</returns>
        /// <example>
        /// ShortHash("hello world") => "5eb63bbb" (first 8 chars of MD5 hex)
        /// </example>
        public static string ShortHash(string input, int length = 8)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (length < 1 || length > 32)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be between 1 and 32.");

            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var hex = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                return hex.Substring(0, Math.Min(length, hex.Length));
            }
        }

        /// <summary>
        /// Generates a random string of the specified length using the given character set.
        /// </summary>
        /// <param name="length">The desired length of the random string.</param>
        /// <param name="chars">The character pool to pick from. Defaults to alphanumeric characters.</param>
        /// <returns>A random string.</returns>
        /// <example>
        /// GenerateRandom(12) => "aB3xK9mP2qLw" (random alphanumeric)
        /// </example>
        public static string GenerateRandom(int length, string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (string.IsNullOrEmpty(chars))
                throw new ArgumentException("Character set must not be empty.", nameof(chars));

            if (length == 0)
                return string.Empty;

            var random = new Random();
            var result = new char[length];

            for (int i = 0; i < length; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }

            return new string(result);
        }

        /// <summary>
        /// Checks whether a string is a valid email format using a simple regex pattern.
        /// This is a basic format check, not a full RFC 5322 validator.
        /// </summary>
        /// <example>
        /// IsValidEmail("user@example.com") => true
        /// IsValidEmail("not-an-email") => false
        /// </example>
        public static bool IsValidEmail(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return System.Text.RegularExpressions.Regex.IsMatch(
                value,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Extracts initials from a full name (first letter of each word, uppercased).
        /// </summary>
        /// <param name="name">The full name.</param>
        /// <param name="maxInitials">Maximum number of initials to return. Defaults to 3.</param>
        /// <example>
        /// GetInitials("John Michael Doe") => "JMD"
        /// GetInitials("Alice") => "A"
        /// </example>
        public static string GetInitials(string name, int maxInitials = 3)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var words = name.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var initials = words
                .Where(w => w.Length > 0)
                .Select(w => char.ToUpperInvariant(w[0]))
                .Take(maxInitials);

            return new string(initials.ToArray());
        }
    }
}
