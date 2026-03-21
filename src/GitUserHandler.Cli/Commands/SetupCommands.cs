using ConsoleAppFramework;
using GitUserHandler.Cli.Services;
using Spectre.Console;

namespace GitUserHandler.Cli.Commands;

public sealed class SetupCommands
{
    private static AppTheme Theme => AppTheme.Default;

    /// <summary>Move ~/.git* files to ~/.git/ and set GIT_CONFIG_GLOBAL.</summary>
    [Command("setup")]
    public async Task Setup()
    {
        var service = ServiceFactory.CreateSetupService();
        var status = service.GetStatus();

        if (status.IsFullySetUp)
        {
            AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Already set up.");
            AnsiConsole.MarkupLine($"  [{Theme.Muted}]Config directory:[/] [{Theme.Emphasis}]{Markup.Escape(service.TargetDir)}[/]");
            foreach (var file in status.FilesAlreadyMoved)
            {
                AnsiConsole.MarkupLine($"  [{Theme.Muted}]{Markup.Escape(Path.GetFileName(file))}[/]");
            }
            AnsiConsole.MarkupLine($"  [{Theme.Muted}]GIT_CONFIG_GLOBAL is set.[/]");
            return;
        }

        if (status.FilesAlreadyMoved.Count > 0)
        {
            AnsiConsole.MarkupLine($"  [{Theme.Muted}]Already in[/] [{Theme.Emphasis}]{Markup.Escape(service.TargetDir)}[/][{Theme.Muted}]:[/]");
            foreach (var file in status.FilesAlreadyMoved)
            {
                AnsiConsole.MarkupLine($"    [{Theme.Muted}]{Markup.Escape(Path.GetFileName(file))}[/]");
            }
        }

        if (status.EnvVarAlreadySet)
        {
            AnsiConsole.MarkupLine($"  [{Theme.Muted}]GIT_CONFIG_GLOBAL already set.[/]");
        }

        var moved = await service.RunSetupAsync();

        foreach (var (source, destination) in moved)
        {
            AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Moved [{Theme.Emphasis}]{Markup.Escape(Path.GetFileName(source))}[/] to [{Theme.Emphasis}]{Markup.Escape(destination)}[/]");
        }

        if (!status.EnvVarAlreadySet)
        {
            AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Set [{Theme.Command}]GIT_CONFIG_GLOBAL[/] = [{Theme.Emphasis}]{Markup.Escape(service.GitConfigTargetPath)}[/]");
        }

        // Check for existing user — label is required, Ctrl+C aborts and rolls back
        var profileCreated = await PromptForUserProfileAsync(service);
        if (!profileCreated)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[{Theme.Warning}]Setup aborted.[/] [{Theme.Muted}]Rolling back...[/]");
            var rolledBack = await service.RunResetAsync();
            foreach (var (src, dst) in rolledBack)
            {
                AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Restored [{Theme.Emphasis}]{Markup.Escape(Path.GetFileName(src))}[/] to [{Theme.Emphasis}]{Markup.Escape(dst)}[/]");
            }
            AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Removed [{Theme.Command}]GIT_CONFIG_GLOBAL[/]");
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[{Theme.Warning}]Note:[/] [{Theme.Muted}]You may need to restart your terminal for the environment variable to take effect.[/]");
    }

    /// <summary>Move ~/.git/.git* files back to ~/ and remove GIT_CONFIG_GLOBAL.</summary>
    [Command("reset")]
    public async Task Reset()
    {
        var service = ServiceFactory.CreateSetupService();
        var status = service.GetStatus();

        if (status.FilesAlreadyMoved.Count == 0 && !status.EnvVarAlreadySet)
        {
            AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Already in default state. Nothing to reset.");
            return;
        }

        var moved = await service.RunResetAsync();

        foreach (var (source, destination) in moved)
        {
            AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Moved [{Theme.Emphasis}]{Markup.Escape(Path.GetFileName(source))}[/] back to [{Theme.Emphasis}]{Markup.Escape(destination)}[/]");
        }

        if (status.EnvVarAlreadySet)
        {
            AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Removed [{Theme.Command}]GIT_CONFIG_GLOBAL[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[{Theme.Warning}]Note:[/] [{Theme.Muted}]You may need to restart your terminal for the environment variable change to take effect.[/]");
    }

    /// <summary>
    /// Prompts the user to label an existing git user. Returns false if aborted (Ctrl+C).
    /// </summary>
    private static async Task<bool> PromptForUserProfileAsync(SetupService service)
    {
        var existingUser = await service.GetExistingUserAsync();
        if (existingUser is null)
            return true;

        var (name, email) = existingUser.Value;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[{Theme.Heading}]Existing git user found[/]");
        AnsiConsole.MarkupLine($"  [{Theme.Muted}]name:[/]  [{Theme.Emphasis}]{Markup.Escape(name)}[/]");
        AnsiConsole.MarkupLine($"  [{Theme.Muted}]email:[/] [{Theme.Emphasis}]{Markup.Escape(email)}[/]");
        AnsiConsole.WriteLine();

        string term;
        try
        {
            term = AnsiConsole.Prompt(
                new TextPrompt<string>($"[{Theme.Command}]Enter a label for this user profile[/] [{Theme.Muted}](e.g. work, personal)[/]:")
                    .Validate(input => !string.IsNullOrWhiteSpace(input)
                        ? ValidationResult.Success()
                        : ValidationResult.Error($"[{Theme.Error}]A label is required.[/]")));
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        string credentialHost;
        try
        {
            credentialHost = AnsiConsole.Prompt(
                new TextPrompt<string>($"[{Theme.Command}]Credential host[/] [{Theme.Muted}](leave empty to skip)[/]:")
                    .DefaultValue("https://github.com")
                    .AllowEmpty());
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        bool gpgSign;
        string? gpgFormat = null;
        string? signingKey = null;
        try
        {
            gpgSign = AnsiConsole.Confirm($"[{Theme.Command}]Enable commit signing?[/]", defaultValue: false);
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
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        term = term.Trim().ToLowerInvariant();
        var credHost = string.IsNullOrWhiteSpace(credentialHost) ? null : credentialHost.Trim();
        var path = await service.CreateUserProfileConfigAsync(term, name, email, credHost, gpgSign, gpgFormat, signingKey);
        AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Created [{Theme.Emphasis}]{Markup.Escape(Path.GetFileName(path))}[/] in [{Theme.Emphasis}]{Markup.Escape(service.TargetDir)}[/]");
        return true;
    }
}
