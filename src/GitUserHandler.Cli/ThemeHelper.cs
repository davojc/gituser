using Spectre.Console;

namespace GitUserHandler.Cli;

internal static class ThemeHelper
{
    /// <summary>
    /// Extracts the foreground Color from a Spectre markup-style string (e.g. "bold dodgerblue2").
    /// Falls back to Color.Default if the string cannot be parsed.
    /// </summary>
    public static Color ParseColor(string styleMarkup)
    {
        if (Style.TryParse(styleMarkup, out var style) && style is not null && style.Foreground != Color.Default)
            return style.Foreground;

        return Color.Default;
    }

    /// <summary>
    /// Parses a Spectre markup-style string into a Style object.
    /// Falls back to Style.Plain if the string cannot be parsed.
    /// </summary>
    public static Style ParseStyle(string styleMarkup)
    {
        return Style.TryParse(styleMarkup, out var style) && style is not null ? style : Style.Plain;
    }
}
