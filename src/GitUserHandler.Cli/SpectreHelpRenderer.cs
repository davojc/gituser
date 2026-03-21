using Spectre.Console;

namespace GitUserHandler.Cli;

internal static class SpectreHelpRenderer
{
    private static AppTheme Theme => AppTheme.Default;

    private static readonly Dictionary<string, (string Heading, int Order)> CommandGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        ["setup"]     = ("Global Config", 1),
        ["reset"]     = ("Global Config", 1),
        ["add"]       = ("Global Config", 1),
        ["edit"]      = ("Global Config", 1),
        ["list"]      = ("Global Config", 1),
        ["apply"]     = ("Repository", 2),
        ["clear"]     = ("Repository", 2),
        ["current"]   = ("Repository", 2),
        ["localsign"] = ("Repository", 2),
        ["update"]    = ("CLI", 3),
    };

    public static void Render(string message)
    {
        var lines = message.Split('\n');
        var inCommandsSection = false;
        var commandLines = new List<(string Name, string Line)>();

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
                inCommandsSection = true;
            }
            else if (line == "Options:")
            {
                // Flush commands before options
                if (inCommandsSection)
                {
                    RenderGroupedCommands(commandLines);
                    inCommandsSection = false;
                }
                AnsiConsole.MarkupLine($"[{Theme.Heading}]Options[/]");
            }
            else if (inCommandsSection && line.StartsWith("  ") && line.TrimStart().Length > 0)
            {
                var trimmed = line.TrimStart();
                var cmdName = trimmed.Split(' ', 2)[0];
                commandLines.Add((cmdName, line));
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

        // If commands section was the last thing, flush it
        if (inCommandsSection)
        {
            RenderGroupedCommands(commandLines);
        }
    }

    public static void RenderError(string message)
    {
        AnsiConsole.MarkupLine($"[{Theme.Error}]Error:[/] {Markup.Escape(message)}");
    }

    private static void RenderGroupedCommands(List<(string Name, string Line)> commandLines)
    {
        var grouped = commandLines
            .GroupBy(c => CommandGroups.TryGetValue(c.Name, out var g) ? g : ("Other", 99))
            .OrderBy(g => g.Key.Item2);

        foreach (var group in grouped)
        {
            AnsiConsole.MarkupLine($"[{Theme.Heading}]{Markup.Escape(group.Key.Item1)}[/]");
            foreach (var cmd in group)
            {
                RenderDetailLine(cmd.Line);
            }
            AnsiConsole.WriteLine();
        }
    }

    private static void RenderDetailLine(string line)
    {
        var trimmed = line.TrimStart();

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
