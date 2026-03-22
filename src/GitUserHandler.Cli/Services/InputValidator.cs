using System.Text.RegularExpressions;

namespace GitUserHandler.Cli.Services;

internal static partial class InputValidator
{
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9\-]*$")]
    private static partial Regex LabelPattern();

    public static bool IsValidLabel(string label) =>
        !string.IsNullOrWhiteSpace(label) && label.Length <= 64 && LabelPattern().IsMatch(label);

    /// <summary>
    /// Escapes a value for safe inclusion in a git config file.
    /// Handles backslashes, quotes, and newlines.
    /// </summary>
    public static string EscapeGitConfigValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Reject newlines entirely — they cannot appear in git config values
        if (value.Contains('\n') || value.Contains('\r'))
            throw new ArgumentException("Git config values cannot contain newlines.", nameof(value));

        // Escape backslashes and quotes
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    /// <summary>
    /// Escapes a value for safe inclusion in a shell export statement.
    /// Prevents command injection via $(), backticks, etc.
    /// </summary>
    public static string EscapeShellValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Use single quotes to prevent all shell interpretation.
        // The only character that needs handling inside single quotes is a single quote itself.
        // Replace ' with '\'' (end quote, escaped literal quote, start quote)
        return "'" + value.Replace("'", "'\\''") + "'";
    }
}
