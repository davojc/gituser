using System.Diagnostics;
using ConsoleAppFramework;
using Spectre.Console;

namespace GitUserHandler.Cli.Commands;

public sealed class UserCommands
{
    private static AppTheme Theme => AppTheme.Default;

    /// <summary>List all configured git user profiles.</summary>
    [Command("list")]
    public async Task List()
    {
        var service = ServiceFactory.CreateSetupService();
        var profiles = await service.GetUserProfilesAsync();

        if (profiles.Count == 0)
        {
            AnsiConsole.MarkupLine($"[{Theme.Warning}]No profiles configured yet.[/] Use [{Theme.Command}]add[/] to create one.");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(ThemeHelper.ParseColor(Theme.TableBorder))
            .AddColumn(new TableColumn($"[{Theme.TableHeader}]Label[/]"))
            .AddColumn(new TableColumn($"[{Theme.TableHeader}]Username[/]"))
            .AddColumn(new TableColumn($"[{Theme.TableHeader}]Email[/]"));

        foreach (var (label, name, email) in profiles)
        {
            table.AddRow(
                Markup.Escape(label),
                Markup.Escape(name),
                Markup.Escape(email));
        }

        AnsiConsole.Write(table);
    }

    /// <summary>Apply a user profile to the current git repository.</summary>
    [Command("apply")]
    public async Task Apply()
    {
        var service = ServiceFactory.CreateSetupService();
        var cwd = Directory.GetCurrentDirectory();

        if (!service.IsGitRepo(cwd))
        {
            AnsiConsole.MarkupLine($"[{Theme.Error}]Not a git repository root.[/] [{Theme.Muted}]Run this command from the root of a git repo.[/]");
            return;
        }

        var profiles = await service.GetUserProfilesAsync();
        if (profiles.Count == 0)
        {
            AnsiConsole.MarkupLine($"[{Theme.Warning}]No profiles configured yet.[/] Use [{Theme.Command}]add[/] to create one first.");
            return;
        }

        var profileMap = profiles.ToDictionary(
            p => $"{p.Label}  {p.Name} <{p.Email}>",
            p => p.Label);

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[{Theme.Command}]Select a user profile[/]:")
                .HighlightStyle(ThemeHelper.ParseStyle(Theme.Command))
                .AddChoices(profileMap.Keys));

        var selectedLabel = profileMap[selected];
        var existingLabel = await service.GetExistingIncludeIfLabelAsync(cwd);

        if (existingLabel is not null && !string.Equals(existingLabel, selectedLabel, StringComparison.OrdinalIgnoreCase))
        {
            var existingProfile = profiles.FirstOrDefault(p => string.Equals(p.Label, existingLabel, StringComparison.OrdinalIgnoreCase));
            AnsiConsole.MarkupLine($"[{Theme.Warning}]Warning:[/] This repo is currently configured with [{Theme.Emphasis}]{Markup.Escape(existingLabel)}[/] ({Markup.Escape(existingProfile.Name)} <{Markup.Escape(existingProfile.Email)}>).");
            AnsiConsole.MarkupLine($"  [{Theme.Muted}]Overwriting with[/] [{Theme.Emphasis}]{Markup.Escape(selectedLabel)}[/].");
        }

        await service.InsertIncludeIfAsync(cwd, selectedLabel);
        AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Inserted [{Theme.Command}]includeIf[/] for [{Theme.Emphasis}]{Markup.Escape(cwd)}[/] using profile [{Theme.Emphasis}]{Markup.Escape(selectedLabel)}[/].");
    }

    /// <summary>Add a new git user profile.</summary>
    [Command("add")]
    public async Task Add()
    {
        var service = ServiceFactory.CreateSetupService();

        var name = AnsiConsole.Prompt(
            new TextPrompt<string>($"[{Theme.Command}]Git username[/]:")
                .Validate(input => !string.IsNullOrWhiteSpace(input)
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"[{Theme.Error}]Username is required.[/]")));

        var email = AnsiConsole.Prompt(
            new TextPrompt<string>($"[{Theme.Command}]Email address[/]:")
                .Validate(input => !string.IsNullOrWhiteSpace(input)
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"[{Theme.Error}]Email is required.[/]")));

        var label = AnsiConsole.Prompt(
            new TextPrompt<string>($"[{Theme.Command}]Label[/] [{Theme.Muted}](e.g. work, personal)[/]:")
                .Validate(input => !string.IsNullOrWhiteSpace(input)
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"[{Theme.Error}]Label is required.[/]")));

        var credentialHost = AnsiConsole.Prompt(
            new TextPrompt<string>($"[{Theme.Command}]Credential host[/] [{Theme.Muted}](leave empty to skip)[/]:")
                .DefaultValue("https://github.com")
                .AllowEmpty());

        var gpgSign = AnsiConsole.Confirm($"[{Theme.Command}]Enable commit signing?[/]", defaultValue: false);
        string? gpgFormat = null;
        string? signingKey = null;

        if (gpgSign)
        {
            gpgFormat = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[{Theme.Command}]Signing format[/]:")
                    .HighlightStyle(ThemeHelper.ParseStyle(Theme.Command))
                    .AddChoices("ssh", "gpg", "x509"));

            signingKey = AnsiConsole.Prompt(
                new TextPrompt<string>($"[{Theme.Command}]Signing key path[/] [{Theme.Muted}](e.g. ~/.ssh/id_ed25519.pub)[/]:")
                    .Validate(input => !string.IsNullOrWhiteSpace(input)
                        ? ValidationResult.Success()
                        : ValidationResult.Error($"[{Theme.Error}]Signing key is required when signing is enabled.[/]")));
        }

        name = name.Trim();
        email = email.Trim();
        label = label.Trim().ToLowerInvariant();
        credentialHost = string.IsNullOrWhiteSpace(credentialHost) ? null : credentialHost.Trim();

        var conflict = await service.CheckForConflictAsync(label, name, email);
        if (conflict is not null)
        {
            AnsiConsole.MarkupLine($"[{Theme.Error}]{Markup.Escape(conflict)}[/]");
            return;
        }

        var path = await service.CreateUserProfileConfigAsync(label, name, email, credentialHost, gpgSign, gpgFormat, signingKey);
        AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Created [{Theme.Emphasis}]{Markup.Escape(Path.GetFileName(path))}[/] in [{Theme.Emphasis}]{Markup.Escape(service.TargetDir)}[/]");
    }

    /// <summary>Edit an existing git user profile.</summary>
    [Command("edit")]
    public async Task Edit()
    {
        var service = ServiceFactory.CreateSetupService();
        var profiles = await service.GetUserProfilesAsync();

        if (profiles.Count == 0)
        {
            AnsiConsole.MarkupLine($"[{Theme.Warning}]No profiles configured yet.[/] Use [{Theme.Command}]add[/] to create one first.");
            return;
        }

        var profileMap = profiles.ToDictionary(
            p => $"{p.Label}  {p.Name} <{p.Email}>",
            p => p.Label);

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[{Theme.Command}]Select a profile to edit[/]:")
                .HighlightStyle(ThemeHelper.ParseStyle(Theme.Command))
                .AddChoices(profileMap.Keys));

        var selectedLabel = profileMap[selected];
        var details = await service.GetProfileDetailsAsync(selectedLabel);
        if (details is null)
        {
            AnsiConsole.MarkupLine($"[{Theme.Error}]Could not read profile '{Markup.Escape(selectedLabel)}'.[/]");
            return;
        }

        var d = details;

        var name = AnsiConsole.Prompt(
            new TextPrompt<string>($"[{Theme.Command}]Git username[/]:")
                .DefaultValue(d.Name)
                .Validate(input => !string.IsNullOrWhiteSpace(input)
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"[{Theme.Error}]Username is required.[/]")));

        var email = AnsiConsole.Prompt(
            new TextPrompt<string>($"[{Theme.Command}]Email address[/]:")
                .DefaultValue(d.Email)
                .Validate(input => !string.IsNullOrWhiteSpace(input)
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"[{Theme.Error}]Email is required.[/]")));

        var credentialHost = AnsiConsole.Prompt(
            new TextPrompt<string>($"[{Theme.Command}]Credential host[/] [{Theme.Muted}](leave empty to skip)[/]:")
                .DefaultValue(d.CredentialHost ?? "https://github.com")
                .AllowEmpty());

        var gpgSign = AnsiConsole.Confirm($"[{Theme.Command}]Enable commit signing?[/]", defaultValue: d.GpgSign);
        string? gpgFormat = null;
        string? signingKey = null;

        if (gpgSign)
        {
            var formatChoices = new[] { "ssh", "gpg", "x509" };
            var defaultFormat = d.GpgFormat ?? "ssh";
            gpgFormat = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[{Theme.Command}]Signing format[/]:")
                    .HighlightStyle(ThemeHelper.ParseStyle(Theme.Command))
                    .AddChoices(formatChoices)
                    .UseConverter(x => x == defaultFormat ? $"{x} (current)" : x));

            // Strip the " (current)" suffix if present
            if (gpgFormat.EndsWith(" (current)"))
                gpgFormat = gpgFormat[..^" (current)".Length];

            var keyPrompt = new TextPrompt<string>($"[{Theme.Command}]Signing key path[/] [{Theme.Muted}](e.g. ~/.ssh/id_ed25519.pub)[/]:")
                .Validate(input => !string.IsNullOrWhiteSpace(input)
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"[{Theme.Error}]Signing key is required when signing is enabled.[/]"));

            if (!string.IsNullOrWhiteSpace(d.SigningKey))
                keyPrompt.DefaultValue(d.SigningKey);

            signingKey = AnsiConsole.Prompt(keyPrompt);
        }

        var label = AnsiConsole.Prompt(
            new TextPrompt<string>($"[{Theme.Command}]Label[/]:")
                .DefaultValue(selectedLabel)
                .Validate(input => !string.IsNullOrWhiteSpace(input)
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"[{Theme.Error}]Label is required.[/]")));

        name = name.Trim();
        email = email.Trim();
        label = label.Trim().ToLowerInvariant();
        var credHost = string.IsNullOrWhiteSpace(credentialHost) ? null : credentialHost.Trim();

        var conflict = await service.CheckForConflictAsync(label, name, email, excludeLabel: selectedLabel);
        if (conflict is not null)
        {
            AnsiConsole.MarkupLine($"[{Theme.Error}]{Markup.Escape(conflict)}[/]");
            return;
        }

        // If label changed, delete old file and update includeIf references
        if (!string.Equals(label, selectedLabel, StringComparison.OrdinalIgnoreCase))
        {
            service.DeleteProfileConfig(selectedLabel);
            await service.UpdateIncludeIfLabelsAsync(selectedLabel, label);
            AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Updated includeIf references from [{Theme.Emphasis}]{Markup.Escape(selectedLabel)}[/] to [{Theme.Emphasis}]{Markup.Escape(label)}[/]");
        }

        var path = await service.CreateUserProfileConfigAsync(label, name, email, credHost, gpgSign, gpgFormat, signingKey);
        AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Updated [{Theme.Emphasis}]{Markup.Escape(Path.GetFileName(path))}[/] in [{Theme.Emphasis}]{Markup.Escape(service.TargetDir)}[/]");
    }

    /// <summary>Show the current git user configuration and its origin.</summary>
    [Command("current")]
    public async Task Current()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "config --list --show-origin --show-scope",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            AnsiConsole.MarkupLine($"[{Theme.Error}]git config failed:[/] [{Theme.Muted}]{Markup.Escape(error.Trim())}[/]");
            return;
        }

        var relevantKeys = new[] { "user.", "commit.gpgsign", "gpg.", "credential." };
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => relevantKeys.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Select(l => l.TrimEnd('\r'))
            .ToList();

        if (lines.Count == 0)
        {
            AnsiConsole.MarkupLine($"[{Theme.Warning}]No git user or signing configuration found.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(ThemeHelper.ParseColor(Theme.TableBorder))
            .AddColumn(new TableColumn($"[{Theme.TableHeader}]Scope[/]"))
            .AddColumn(new TableColumn($"[{Theme.TableHeader}]Origin[/]"))
            .AddColumn(new TableColumn($"[{Theme.TableHeader}]Key[/]"))
            .AddColumn(new TableColumn($"[{Theme.TableHeader}]Value[/]"));

        foreach (var line in lines)
        {
            // Format: "scope\tfile:path\tkey=value"
            var parts = line.Split('\t');
            if (parts.Length >= 3)
            {
                var scope = parts[0].Trim();
                var origin = parts[1].Trim();
                var keyValue = parts[2];
                var eqIndex = keyValue.IndexOf('=');
                var key = eqIndex >= 0 ? keyValue[..eqIndex] : keyValue;
                var value = eqIndex >= 0 ? keyValue[(eqIndex + 1)..] : "";

                table.AddRow(
                    Markup.Escape(scope),
                    $"[{Theme.Muted}]{Markup.Escape(origin)}[/]",
                    $"[{Theme.Command}]{Markup.Escape(key)}[/]",
                    $"[{Theme.Emphasis}]{Markup.Escape(value)}[/]");
            }
        }

        AnsiConsole.Write(table);
    }

    /// <summary>Set commit signing on or off for the current git repository.</summary>
    [Command("localsign")]
    public async Task LocalSign()
    {
        var service = ServiceFactory.CreateSetupService();
        var cwd = Directory.GetCurrentDirectory();

        if (!service.IsGitRepo(cwd))
        {
            AnsiConsole.MarkupLine($"[{Theme.Error}]Not a git repository root.[/] [{Theme.Muted}]Run this command from the root of a git repo.[/]");
            return;
        }

        var enable = AnsiConsole.Confirm($"[{Theme.Command}]Enable commit signing for this repo?[/]", defaultValue: true);
        var value = enable ? "true" : "false";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"config --local commit.gpgsign {value}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            AnsiConsole.MarkupLine($"[{Theme.Error}]Failed:[/] [{Theme.Muted}]{Markup.Escape(error.Trim())}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Set [{Theme.Command}]commit.gpgsign[/] = [{Theme.Emphasis}]{value}[/] for this repo.");
    }

    /// <summary>Remove the user profile from the current git repository.</summary>
    [Command("clear")]
    public async Task Clear()
    {
        var service = ServiceFactory.CreateSetupService();
        var cwd = Directory.GetCurrentDirectory();

        if (!service.IsGitRepo(cwd))
        {
            AnsiConsole.MarkupLine($"[{Theme.Error}]Not a git repository root.[/] [{Theme.Muted}]Run this command from the root of a git repo.[/]");
            return;
        }

        var existingLabel = await service.GetExistingIncludeIfLabelAsync(cwd);
        if (existingLabel is null)
        {
            AnsiConsole.MarkupLine($"[{Theme.Warning}]No includeIf configured for this directory.[/]");
            return;
        }

        await service.RemoveIncludeIfAsync(cwd);
        AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Removed [{Theme.Command}]includeIf[/] for [{Theme.Emphasis}]{Markup.Escape(cwd)}[/] (was [{Theme.Emphasis}]{Markup.Escape(existingLabel)}[/]).");

        var removeGpgSign = AnsiConsole.Confirm($"[{Theme.Command}]Also remove local commit.gpgsign setting?[/]", defaultValue: true);
        if (removeGpgSign)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "config --local --unset commit.gpgsign",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Exit code 5 means the key didn't exist — that's fine
            if (process.ExitCode is 0 or 5)
            {
                AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Removed local [{Theme.Command}]commit.gpgsign[/] setting.");
            }
        }
    }
}
