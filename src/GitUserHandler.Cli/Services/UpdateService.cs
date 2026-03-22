using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace GitUserHandler.Cli.Services;

public sealed class UpdateService
{
    private const string RepoOwner = "davojc";
    private const string RepoName = "gituser";
    private const string ReleasesUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "gituser-cli" },
            { "Accept", "application/vnd.github+json" }
        }
    };

    public static Version GetCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "0.0.0";

        // Strip any suffix like +commitsha
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];

        return Version.TryParse(version, out var v) ? v : new Version(0, 0, 0);
    }

    public async Task<(Version Version, string? AssetUrl, string? AssetName, string? ChecksumUrl)?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        GitHubRelease? release;
        try
        {
            release = await Http.GetFromJsonAsync(ReleasesUrl, GitHubJsonContext.Default.GitHubRelease, cancellationToken);
        }
        catch
        {
            return null;
        }

        if (release?.TagName is null)
            return null;

        var tagVersion = release.TagName.TrimStart('v');
        if (!Version.TryParse(tagVersion, out var latestVersion))
            return null;

        var currentVersion = GetCurrentVersion();
        if (latestVersion <= currentVersion)
            return null;

        var expectedAssetName = GetExpectedAssetName();
        var asset = release.Assets?.FirstOrDefault(a =>
            string.Equals(a.Name, expectedAssetName, StringComparison.OrdinalIgnoreCase));

        var checksumAsset = release.Assets?.FirstOrDefault(a =>
            string.Equals(a.Name, expectedAssetName + ".sha256", StringComparison.OrdinalIgnoreCase));

        return (latestVersion, asset?.BrowserDownloadUrl, asset?.Name, checksumAsset?.BrowserDownloadUrl);
    }

    public async Task<string> DownloadAndExtractAsync(string assetUrl, string assetName, string? checksumUrl = null, CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gituser-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var archivePath = Path.Combine(tempDir, assetName);

            using (var response = await Http.GetAsync(assetUrl, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var fs = File.Create(archivePath);
                await response.Content.CopyToAsync(fs, cancellationToken);
            }

            if (checksumUrl is not null)
            {
                await VerifyChecksumAsync(archivePath, checksumUrl, cancellationToken);
            }

            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);

            if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, extractDir);
            }
            else if (assetName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                await ExtractTarGzAsync(archivePath, extractDir, cancellationToken);
            }

            // Find the executable in extracted files
            var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gituser.exe" : "gituser";
            var extractedExe = Directory.GetFiles(extractDir, exeName, SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new FileNotFoundException($"Could not find {exeName} in downloaded archive.");

            return extractedExe;
        }
        catch
        {
            // Clean up temp directory on failure
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
            throw;
        }
    }

    public static void ReplaceCurrentBinary(string newBinaryPath)
    {
        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine current executable path.");

        var backupPath = currentExe + ".bak";

        // On Windows, rename the running exe (allowed), then copy new one in place
        if (File.Exists(backupPath))
            File.Delete(backupPath);

        File.Move(currentExe, backupPath);

        try
        {
            File.Copy(newBinaryPath, currentExe);

            // On Unix, preserve executable permission
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(currentExe,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            // Clean up backup
            try { File.Delete(backupPath); } catch { /* best effort */ }
        }
        catch
        {
            // Rollback: restore backup
            if (File.Exists(backupPath) && !File.Exists(currentExe))
                File.Move(backupPath, currentExe);
            throw;
        }
    }

    private static async Task VerifyChecksumAsync(string filePath, string checksumUrl, CancellationToken cancellationToken)
    {
        var checksumText = await Http.GetStringAsync(checksumUrl, cancellationToken);
        var expectedHash = checksumText.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim().ToLowerInvariant();

        var actualHash = await ComputeSha256Async(filePath, cancellationToken);

        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Checksum verification failed. Expected: {expectedHash}, Got: {actualHash}");
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hashBytes);
    }

    internal static string GetExpectedAssetName()
    {
        var rid = GetCurrentRid();
        return rid.StartsWith("win") ? $"gituser-{rid}.zip" : $"gituser-{rid}.tar.gz";
    }

    internal static string GetCurrentRid()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"win-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"osx-{arch}";
        return $"linux-{arch}";
    }

    private static async Task ExtractTarGzAsync(string archivePath, string extractDir, CancellationToken cancellationToken)
    {
        await using var fileStream = File.OpenRead(archivePath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        await TarExtractAsync(gzipStream, extractDir, cancellationToken);
    }

    private static async Task TarExtractAsync(Stream stream, string outputDir, CancellationToken cancellationToken)
    {
        // Simple tar reader — handles regular files only (sufficient for single-binary archives)
        var buffer = new byte[512];

        while (true)
        {
            var bytesRead = await ReadExactAsync(stream, buffer, cancellationToken);
            if (bytesRead < 512 || buffer.All(b => b == 0))
                break;

            var name = GetTarString(buffer, 0, 100).TrimEnd('/');
            var sizeStr = GetTarString(buffer, 124, 12).Trim();
            var size = Convert.ToInt64(sizeStr, 8);
            var typeFlag = (char)buffer[156];

            // Reject symlinks and other non-regular file types
            if (typeFlag is not ('0' or '\0'))
            {
                if (size > 0)
                {
                    var totalBlocks = (size + 511) / 512 * 512;
                    var skip = new byte[8192];
                    var rem = totalBlocks;
                    while (rem > 0)
                    {
                        var toRead = (int)Math.Min(skip.Length, rem);
                        var read = await stream.ReadAsync(skip.AsMemory(0, toRead), cancellationToken);
                        if (read == 0) break;
                        rem -= read;
                    }
                }
                continue;
            }

            if (size > 0 && !string.IsNullOrEmpty(name))
            {
                var safeName = Path.GetFileName(name);
                if (string.IsNullOrEmpty(safeName) || safeName.Contains(".."))
                    throw new InvalidOperationException($"Tar entry has unsafe filename: {name}");

                var outputPath = Path.Combine(outputDir, safeName);
                // Verify the resolved path is within the output directory
                var fullOutput = Path.GetFullPath(outputPath);
                var fullDir = Path.GetFullPath(outputDir + Path.DirectorySeparatorChar);
                if (!fullOutput.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Tar entry would extract outside target directory: {name}");

                await using var outFile = File.Create(outputPath);
                var remaining = size;
                var readBuf = new byte[8192];
                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(readBuf.Length, remaining);
                    var read = await stream.ReadAsync(readBuf.AsMemory(0, toRead), cancellationToken);
                    if (read == 0) break;
                    await outFile.WriteAsync(readBuf.AsMemory(0, read), cancellationToken);
                    remaining -= read;
                }

                // Tar pads to 512-byte blocks
                var padding = (512 - (int)(size % 512)) % 512;
                if (padding > 0)
                    await ReadExactAsync(stream, new byte[padding], cancellationToken);
            }
            else if (size > 0)
            {
                // Skip entries with no name
                var totalBlocks = (size + 511) / 512 * 512;
                var skipBuf = new byte[8192];
                var remaining = totalBlocks;
                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(skipBuf.Length, remaining);
                    var read = await stream.ReadAsync(skipBuf.AsMemory(0, toRead), cancellationToken);
                    if (read == 0) break;
                    remaining -= read;
                }
            }
        }
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken);
            if (read == 0) break;
            totalRead += read;
        }
        return totalRead;
    }

    private static string GetTarString(byte[] buffer, int offset, int length) =>
        System.Text.Encoding.ASCII.GetString(buffer, offset, length).TrimEnd('\0');
}

public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
}

[JsonSerializable(typeof(GitHubRelease))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
