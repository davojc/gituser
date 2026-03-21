namespace GitUserHandler.Cli;

public sealed class AppTheme
{
    public string Heading { get; init; } = "bold underline dodgerblue2";
    public string Command { get; init; } = "green";
    public string Description { get; init; } = "dim";
    public string Success { get; init; } = "green";
    public string Warning { get; init; } = "yellow";
    public string Error { get; init; } = "bold red";
    public string Emphasis { get; init; } = "bold";
    public string Muted { get; init; } = "dim";
    public string Prompt { get; init; } = "dim";
    public string UsageText { get; init; } = "white";
    public string TableBorder { get; init; } = "dodgerblue2";
    public string TableHeader { get; init; } = "bold dodgerblue2";

    public static AppTheme Default { get; } = new();
}
