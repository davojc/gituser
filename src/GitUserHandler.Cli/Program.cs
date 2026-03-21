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
