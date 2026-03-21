using ConsoleAppFramework;
using GitUserHandler.Cli.Services;
using Spectre.Console;

namespace GitUserHandler.Cli.Commands;

public sealed class UpdateCommands
{
    private static AppTheme Theme => AppTheme.Default;

    /// <summary>Check for updates and install the latest version.</summary>
    [Command("update")]
    public async Task Update()
    {
        var service = new UpdateService();
        var currentVersion = UpdateService.GetCurrentVersion();

        AnsiConsole.MarkupLine($"[{Theme.Muted}]Current version:[/] [{Theme.Emphasis}]{Markup.Escape(currentVersion.ToString())}[/]");
        AnsiConsole.MarkupLine($"[{Theme.Muted}]Checking for updates...[/]");

        var result = await service.CheckForUpdateAsync();

        if (result is null)
        {
            AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] You are on the latest version.");
            return;
        }

        var (latestVersion, assetUrl, assetName) = result.Value;

        if (assetUrl is null || assetName is null)
        {
            AnsiConsole.MarkupLine($"[{Theme.Warning}]Version [{Theme.Emphasis}]{Markup.Escape(latestVersion.ToString())}[/] is available but no matching binary found for your platform.[/]");
            AnsiConsole.MarkupLine($"  [{Theme.Muted}]Expected:[/] [{Theme.Emphasis}]{Markup.Escape(UpdateService.GetExpectedAssetName())}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[{Theme.Success}]New version available:[/] [{Theme.Emphasis}]{Markup.Escape(latestVersion.ToString())}[/]");

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(ThemeHelper.ParseStyle(Theme.Command))
            .StartAsync("Downloading...", async ctx =>
            {
                var newBinary = await service.DownloadAndExtractAsync(assetUrl, assetName);

                ctx.Status("Installing...");
                UpdateService.ReplaceCurrentBinary(newBinary);
            });

        AnsiConsole.MarkupLine($"[{Theme.Success}]\u2713[/] Updated to [{Theme.Emphasis}]{Markup.Escape(latestVersion.ToString())}[/]");
    }
}
