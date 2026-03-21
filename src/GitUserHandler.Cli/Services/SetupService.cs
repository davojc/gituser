using System.Text.RegularExpressions;

namespace GitUserHandler.Cli.Services;

public sealed class SetupService(IEnvironmentProvider environment)
{
    private const string EnvVarName = "GIT_CONFIG_GLOBAL";

    public string TargetDir => Path.Combine(environment.HomeDirectory, ".git");
    public string GitConfigTargetPath => Path.Combine(TargetDir, ".gitconfig");

    public SetupStatus GetStatus()
    {
        var envValue = environment.GetEnvironmentVariable(EnvVarName);
        var envAlreadySet = !string.IsNullOrEmpty(envValue)
            && string.Equals(Path.GetFullPath(envValue), Path.GetFullPath(GitConfigTargetPath), StringComparison.OrdinalIgnoreCase);

        var homeGitFiles = environment.GetFiles(environment.HomeDirectory, ".git*");
        var filesToMove = homeGitFiles
            .Where(f => !environment.DirectoryExists(f))
            .ToList();

        var alreadyMoved = environment.DirectoryExists(TargetDir)
            ? environment.GetFiles(TargetDir, ".git*").ToList()
            : [];

        return new SetupStatus
        {
            EnvVarAlreadySet = envAlreadySet,
            FilesToMove = filesToMove,
            FilesAlreadyMoved = alreadyMoved
        };
    }

    public async Task<IReadOnlyList<(string Source, string Destination)>> RunSetupAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.DirectoryExists(TargetDir))
            environment.CreateDirectory(TargetDir);

        var status = GetStatus();
        var moved = new List<(string Source, string Destination)>();

        foreach (var sourceFile in status.FilesToMove)
        {
            var fileName = Path.GetFileName(sourceFile);
            var destination = Path.Combine(TargetDir, fileName);
            environment.MoveFile(sourceFile, destination);
            moved.Add((sourceFile, destination));
        }

        await environment.SetPersistentEnvironmentVariableAsync(EnvVarName, GitConfigTargetPath, cancellationToken);

        return moved;
    }

    public async Task<(string Name, string Email)?> GetExistingUserAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.FileExists(GitConfigTargetPath))
            return null;

        var content = await environment.ReadFileAsync(GitConfigTargetPath, cancellationToken);
        return ParseGitConfigUser(content);
    }

    public async Task<string> CreateUserProfileConfigAsync(string term, string name, string email, CancellationToken cancellationToken = default)
    {
        var fileName = $".gitconfig-{term}";
        var filePath = Path.Combine(TargetDir, fileName);
        var content = $"[user]{Environment.NewLine}\tname = {name}{Environment.NewLine}\temail = {email}{Environment.NewLine}";
        await environment.WriteFileAsync(filePath, content, cancellationToken);
        return filePath;
    }

    internal static (string Name, string Email)? ParseGitConfigUser(string content)
    {
        string? name = null;
        string? email = null;
        var inUserSection = false;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith('['))
            {
                inUserSection = line.StartsWith("[user]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inUserSection)
                continue;

            var match = Regex.Match(line, @"^name\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                name = match.Groups[1].Value.Trim();
                continue;
            }

            match = Regex.Match(line, @"^email\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                email = match.Groups[1].Value.Trim();
            }
        }

        return name is not null && email is not null ? (name, email) : null;
    }

    public async Task<IReadOnlyList<(string Label, string Name, string Email)>> GetUserProfilesAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.DirectoryExists(TargetDir))
            return [];

        var files = environment.GetFiles(TargetDir, ".gitconfig-*");
        var profiles = new List<(string Label, string Name, string Email)>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var label = fileName[".gitconfig-".Length..];
            var content = await environment.ReadFileAsync(file, cancellationToken);
            var user = ParseGitConfigUser(content);
            if (user is not null)
                profiles.Add((label, user.Value.Name, user.Value.Email));
        }

        return profiles;
    }

    public async Task<string?> CheckForConflictAsync(string label, string name, string email, CancellationToken cancellationToken = default)
    {
        var profiles = await GetUserProfilesAsync(cancellationToken);

        foreach (var p in profiles)
        {
            if (string.Equals(p.Label, label, StringComparison.OrdinalIgnoreCase))
                return $"Label '{label}' is already in use by {p.Name} <{p.Email}>.";
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return $"Username '{name}' is already in use with label '{p.Label}'.";
            if (string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase))
                return $"Email '{email}' is already in use with label '{p.Label}'.";
        }

        return null;
    }

    public bool IsGitRepo(string directory) =>
        environment.DirectoryExists(Path.Combine(directory, ".git"));

    public async Task<string?> GetExistingIncludeIfLabelAsync(string repoDir, CancellationToken cancellationToken = default)
    {
        if (!environment.FileExists(GitConfigTargetPath))
            return null;

        var content = await environment.ReadFileAsync(GitConfigTargetPath, cancellationToken);
        var gitDir = NormalizeGitDir(repoDir);
        string? currentSection = null;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith('['))
            {
                currentSection = line;
                continue;
            }

            if (currentSection is not null
                && currentSection.StartsWith("[includeIf", StringComparison.OrdinalIgnoreCase)
                && currentSection.Contains(gitDir, StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(line, @"^\s*path\s*=\s*(.+)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var path = match.Groups[1].Value.Trim();
                    var fileName = Path.GetFileName(path);
                    if (fileName.StartsWith(".gitconfig-"))
                        return fileName[".gitconfig-".Length..];
                }
            }
        }

        return null;
    }

    public async Task InsertIncludeIfAsync(string repoDir, string label, CancellationToken cancellationToken = default)
    {
        var gitDir = NormalizeGitDir(repoDir);
        var profilePath = Path.Combine(TargetDir, $".gitconfig-{label}").Replace('\\', '/');
        var sectionHeader = $"[includeIf \"gitdir:{gitDir}\"]";
        var pathLine = $"\tpath = {profilePath}";

        if (!environment.FileExists(GitConfigTargetPath))
        {
            await environment.WriteFileAsync(GitConfigTargetPath,
                sectionHeader + Environment.NewLine + pathLine + Environment.NewLine,
                cancellationToken);
            return;
        }

        var content = await environment.ReadFileAsync(GitConfigTargetPath, cancellationToken);
        var lines = content.Split('\n').ToList();
        var replaced = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[includeIf", StringComparison.OrdinalIgnoreCase)
                && trimmed.Contains(gitDir, StringComparison.OrdinalIgnoreCase))
            {
                for (var j = i + 1; j < lines.Count; j++)
                {
                    var innerTrimmed = lines[j].Trim();
                    if (innerTrimmed.StartsWith('['))
                        break;
                    if (innerTrimmed.StartsWith("path", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[j] = pathLine;
                        replaced = true;
                        break;
                    }
                }
                break;
            }
        }

        if (!replaced)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                lines.Add("");
            lines.Add(sectionHeader);
            lines.Add(pathLine);
        }

        await environment.WriteFileAsync(GitConfigTargetPath,
            string.Join('\n', lines),
            cancellationToken);
    }

    internal static string NormalizeGitDir(string repoDir)
    {
        var normalized = repoDir.Replace('\\', '/');
        if (!normalized.EndsWith('/'))
            normalized += '/';
        return normalized;
    }

    public async Task<IReadOnlyList<(string Source, string Destination)>> RunResetAsync(CancellationToken cancellationToken = default)
    {
        var status = GetStatus();
        var moved = new List<(string Source, string Destination)>();

        foreach (var file in status.FilesAlreadyMoved)
        {
            var fileName = Path.GetFileName(file);
            var destination = Path.Combine(environment.HomeDirectory, fileName);
            environment.MoveFile(file, destination);
            moved.Add((file, destination));
        }

        await environment.RemovePersistentEnvironmentVariableAsync(EnvVarName, cancellationToken);

        return moved;
    }
}
