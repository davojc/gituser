using ConsoleAppFramework;
using GitUserHandler.Cli;
using GitUserHandler.Cli.Services;
using Spectre.Console;

ConsoleApp.Log = SpectreHelpRenderer.Render;
ConsoleApp.LogError = SpectreHelpRenderer.RenderError;

if (args is ["--version" or "-v"])
{
    await VersionCommand.RunAsync();
    return;
}

// Check for updates before running any command (skip for update/help/version flags)
if (args.Length > 0
    && !args[0].Equals("update", StringComparison.OrdinalIgnoreCase)
    && !args.Contains("--help") && !args.Contains("-h"))
{
    await UpdateNotifier.CheckAsync();
}

var app = ConsoleApp.Create();
app.Add<GitUserHandler.Cli.Commands.SetupCommands>();
app.Add<GitUserHandler.Cli.Commands.UpdateCommands>();
app.Add<GitUserHandler.Cli.Commands.UserCommands>();
app.Run(args);

namespace GitUserHandler.Cli
{
    internal static class ServiceFactory
    {
        public static SetupService CreateSetupService() =>
            new(new EnvironmentProvider());
    }

    internal static class UpdateNotifier
    {
        public static async Task CheckAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var service = new UpdateService();
                var result = await service.CheckForUpdateAsync(cts.Token);

                if (result is not null)
                {
                    var theme = AppTheme.Default;
                    AnsiConsole.MarkupLine(
                        $"[{theme.Warning}]Update available:[/] [{theme.Emphasis}]{Markup.Escape(result.Value.Version.ToString())}[/]  " +
                        $"[{theme.Muted}]Run[/] [{theme.Command}]gituser update[/] [{theme.Muted}]to install.[/]");
                    AnsiConsole.WriteLine();
                }
            }
            catch
            {
                // Silently ignore — don't delay the user's command
            }
        }
    }

    internal static class VersionCommand
    {
        public static async Task RunAsync()
        {
            var theme = AppTheme.Default;
            var currentVersion = UpdateService.GetCurrentVersion();

            AnsiConsole.MarkupLine($"[{theme.Emphasis}]gituser[/] [{theme.Command}]{Markup.Escape(currentVersion.ToString())}[/]");

            try
            {
                var service = new UpdateService();
                var result = await service.CheckForUpdateAsync();

                if (result is null)
                {
                    AnsiConsole.MarkupLine($"[{theme.Muted}]Up to date.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[{theme.Warning}]Update available:[/] [{theme.Emphasis}]{Markup.Escape(result.Value.Version.ToString())}[/]  [{theme.Muted}]Run[/] [{theme.Command}]gituser update[/] [{theme.Muted}]to install.[/]");
                }
            }
            catch
            {
                AnsiConsole.MarkupLine($"[{theme.Muted}]Could not check for updates.[/]");
            }
        }
    }
}
