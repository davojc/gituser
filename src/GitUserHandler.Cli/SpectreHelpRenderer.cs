using Spectre.Console;

namespace GitUserHandler.Cli;

internal static class SpectreHelpRenderer
{
    private static AppTheme Theme => AppTheme.Default;

    public static void Render(string message)
    {
        // ConsoleAppFramework emits help as a single pre-formatted string.
        // Parse sections and re-render with Spectre.Console styling.

        var lines = message.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("Usage:"))
            {
                AnsiConsole.MarkupLine($"[{Theme.Heading}]Usage[/]");
                var usage = line["Usage:".Length..].Trim();
                AnsiConsole.MarkupLine($"  [{Theme.Prompt}]$[/] [{Theme.UsageText}]{Markup.Escape(usage)}[/]");
                AnsiConsole.WriteLine();
            }
            else if (line == "Commands:")
            {
                AnsiConsole.MarkupLine($"[{Theme.Heading}]Commands[/]");
            }
            else if (line == "Options:")
            {
                AnsiConsole.MarkupLine($"[{Theme.Heading}]Options[/]");
            }
            else if (line.StartsWith("  ") && line.TrimStart().Length > 0)
            {
                RenderDetailLine(line);
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                AnsiConsole.MarkupLine($"  [{Theme.Description} italic]{Markup.Escape(line.Trim())}[/]");
                AnsiConsole.WriteLine();
            }
        }
    }

    public static void RenderError(string message)
    {
        AnsiConsole.MarkupLine($"[{Theme.Error}]Error:[/] {Markup.Escape(message)}");
    }

    private static void RenderDetailLine(string line)
    {
        var trimmed = line.TrimStart();

        // Command lines: "  name    description"
        // Option lines:  "  --name <type>    description  [Required]/[Default: x]"
        var parts = System.Text.RegularExpressions.Regex.Split(trimmed, @"\s{2,}");

        if (parts.Length >= 2)
        {
            var name = parts[0];
            var description = string.Join("  ", parts[1..]);

            var tagMatch = System.Text.RegularExpressions.Regex.Match(description, @"\[(Required|Default:\s*[^\]]*)\]$");
            var tag = "";
            var desc = description;
            if (tagMatch.Success)
            {
                tag = tagMatch.Value;
                desc = description[..tagMatch.Index].TrimEnd();
            }

            var markup = $"  [{Theme.Command}]{Markup.Escape(name)}[/]";
            if (!string.IsNullOrEmpty(desc))
                markup += $"  [{Theme.Description}]{Markup.Escape(desc)}[/]";
            if (!string.IsNullOrEmpty(tag))
                markup += tag.Contains("Required")
                    ? $"  [{Theme.Error}]{Markup.Escape(tag)}[/]"
                    : $"  [{Theme.Warning}]{Markup.Escape(tag)}[/]";

            AnsiConsole.MarkupLine(markup);
        }
        else
        {
            AnsiConsole.MarkupLine($"  [{Theme.Command}]{Markup.Escape(trimmed)}[/]");
        }
    }
}
