namespace GitUserHandler.Cli.Services;

public interface IEnvironmentProvider
{
    string HomeDirectory { get; }
    string? GetEnvironmentVariable(string name);
    Task SetPersistentEnvironmentVariableAsync(string name, string value, CancellationToken cancellationToken = default);
    Task RemovePersistentEnvironmentVariableAsync(string name, CancellationToken cancellationToken = default);
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void MoveFile(string source, string destination);
    IReadOnlyList<string> GetFiles(string directory, string searchPattern);
    Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default);
    Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default);
}
