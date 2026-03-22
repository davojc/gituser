using System.Runtime.InteropServices;

namespace GitUserHandler.Cli.Services;

public sealed class EnvironmentProvider : IEnvironmentProvider
{
    public string HomeDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string? GetEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable(name);

    public async Task SetPersistentEnvironmentVariableAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        Environment.SetEnvironmentVariable(name, value);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
        }
        else
        {
            await AppendToShellProfileAsync(name, value, cancellationToken);
        }
    }

    public async Task RemovePersistentEnvironmentVariableAsync(string name, CancellationToken cancellationToken = default)
    {
        Environment.SetEnvironmentVariable(name, null);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.User);
        }
        else
        {
            await RemoveFromShellProfileAsync(name, cancellationToken);
        }
    }

    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void MoveFile(string source, string destination) => File.Move(source, destination);

    public IReadOnlyList<string> GetFiles(string directory, string searchPattern) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory, searchPattern) : [];

    public Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(path, cancellationToken);

    public Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(path, content, cancellationToken);

    private async Task AppendToShellProfileAsync(string name, string value, CancellationToken cancellationToken)
    {
        var profilePath = GetShellProfilePath();

        // Shell-escape the value to prevent command injection
        var escapedValue = InputValidator.EscapeShellValue(value);
        var exportLine = $"export {name}={escapedValue}";

        if (File.Exists(profilePath))
        {
            var content = await File.ReadAllTextAsync(profilePath, cancellationToken);
            if (content.Contains($"export {name}="))
                return;
        }

        await File.AppendAllTextAsync(profilePath, Environment.NewLine + exportLine + Environment.NewLine, cancellationToken);
    }

    private async Task RemoveFromShellProfileAsync(string name, CancellationToken cancellationToken)
    {
        var profilePath = GetShellProfilePath();
        if (!File.Exists(profilePath))
            return;

        var lines = await File.ReadAllLinesAsync(profilePath, cancellationToken);
        var filtered = lines.Where(line => !line.TrimStart().StartsWith($"export {name}=")).ToArray();

        if (filtered.Length != lines.Length)
            await File.WriteAllLinesAsync(profilePath, filtered, cancellationToken);
    }

    private string GetShellProfilePath()
    {
        var home = HomeDirectory;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var zshrc = Path.Combine(home, ".zshrc");
            if (IsRegularFile(zshrc))
                return zshrc;
        }

        var bashrc = Path.Combine(home, ".bashrc");
        if (IsRegularFile(bashrc))
            return bashrc;

        return Path.Combine(home, ".profile");
    }

    private static bool IsRegularFile(string path)
    {
        if (!File.Exists(path))
            return false;

        var info = new FileInfo(path);
        return (info.Attributes & FileAttributes.ReparsePoint) == 0;
    }
}
