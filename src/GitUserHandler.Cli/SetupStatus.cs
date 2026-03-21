namespace GitUserHandler.Cli;

public sealed class SetupStatus
{
    public bool EnvVarAlreadySet { get; init; }
    public IReadOnlyList<string> FilesAlreadyMoved { get; init; } = [];
    public IReadOnlyList<string> FilesToMove { get; init; } = [];

    public bool IsFullySetUp => FilesToMove.Count == 0 && FilesAlreadyMoved.Count > 0 && EnvVarAlreadySet;
}
