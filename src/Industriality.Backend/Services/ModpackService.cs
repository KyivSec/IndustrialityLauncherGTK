using System.IO.Compression;
using System.Text.Json;
using Industriality.Backend.Abstractions;
using Industriality.Backend.Models;

namespace Industriality.Backend.Services;

public sealed class ModpackService : IModpackService
{
    private const string ModpackDownloadUrl = "https://github.com/KyivSec/IndustrialityProject/releases/latest/download/Industriality.NeoForge.zip";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/KyivSec/IndustrialityProject/releases/latest";

    private readonly LauncherPaths _paths;
    private LauncherSettings _settings;
    private int _lastReportedModpackPercent = -1;

    public ModpackService(LauncherSettings settings, LauncherPaths paths)
    {
        _settings = settings;
        _paths = paths;
    }

    public void UpdateSettings(LauncherSettings settings)
    {
        _settings = settings;
    }

    public async Task<ModpackUpdateInfo> GetModpackUpdateInfoAsync(CancellationToken cancellationToken = default)
    {
        var latestVersion = await GetLatestModpackVersionAsync(cancellationToken).ConfigureAwait(false);
        var currentVersion = GetInstalledModpackVersion();
        var isInstalledNow = IsModpackContentInstalled();
        var updateAvailable = ModpackVersionComparer.IsUpdateAvailable(isInstalledNow, currentVersion, latestVersion);

        return new ModpackUpdateInfo(isInstalledNow, currentVersion, latestVersion, updateAvailable);
    }

    public async Task DownloadAndInstallModpackAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();

        if (File.Exists(_paths.ModpackZipPath))
        {
            File.Delete(_paths.ModpackZipPath);
        }

        LauncherShared.ReportProgress(progress, "Modpack", "Downloading modpack.", 84);

        using var client = LauncherShared.CreateGitHubHttpClient();
        using var response = await client.GetAsync(
            ModpackDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalLength = response.Content.Headers.ContentLength;
        _lastReportedModpackPercent = -1;

        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var output = File.Create(_paths.ModpackZipPath))
        {
            var buffer = new byte[1024 * 256];
            long totalRead = 0;
            int read;

            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                totalRead += read;

                if (totalLength.HasValue && totalLength.Value > 0)
                {
                    var ratio = (double)totalRead / totalLength.Value;
                    var percent = (int)Math.Clamp(Math.Floor(84d + (ratio * 8d)), 84, 92);

                    if (percent != _lastReportedModpackPercent)
                    {
                        _lastReportedModpackPercent = percent;
                        LauncherShared.ReportProgress(progress, "Modpack", "Downloading modpack.", percent);
                    }
                }
            }
        }

        LauncherShared.ReportProgress(progress, "Modpack", "Extracting modpack.", 93);
        ExtractMinecraftFolderOnly(_paths.ModpackZipPath, _paths.GameDirectory);

        string installedVersion;
        try
        {
            installedVersion = await GetLatestModpackVersionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            installedVersion = "unknown";
        }

        File.WriteAllText(_paths.ModpackVersionFilePath, installedVersion);
        LauncherShared.ReportProgress(progress, "Modpack", "Modpack installed.", 98);
    }

    public bool IsModpackContentInstalled()
    {
        return Directory.Exists(Path.Combine(_paths.GameDirectory, "mods")) ||
               Directory.Exists(Path.Combine(_paths.GameDirectory, "config")) ||
               Directory.Exists(Path.Combine(_paths.GameDirectory, "kubejs")) ||
               Directory.Exists(Path.Combine(_paths.GameDirectory, "resourcepacks")) ||
               File.Exists(Path.Combine(_paths.GameDirectory, "mmc-pack.json"));
    }

    private static void ExtractMinecraftFolderOnly(string zipPath, string gameDirectory)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName))
            {
                continue;
            }

            var normalizedEntry = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (!normalizedEntry.StartsWith("minecraft/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativeTargetPath = normalizedEntry["minecraft/".Length..];
            if (string.IsNullOrWhiteSpace(relativeTargetPath))
            {
                continue;
            }

            var destinationPath = Path.GetFullPath(Path.Combine(
                gameDirectory,
                relativeTargetPath.Replace('/', Path.DirectorySeparatorChar)));

            var rootPath = Path.GetFullPath(gameDirectory + Path.DirectorySeparatorChar);
            if (!destinationPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Zip entry attempted to escape the game directory: " + entry.FullName);
            }

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private async Task<string> GetLatestModpackVersionAsync(CancellationToken cancellationToken)
    {
        using var client = LauncherShared.CreateGitHubHttpClient();
        using var response = await client.GetAsync(LatestReleaseApiUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var releaseInfo = JsonSerializer.Deserialize<GitHubReleaseInfo>(json);
        if (releaseInfo is null || string.IsNullOrWhiteSpace(releaseInfo.TagName))
        {
            throw new InvalidOperationException("Could not resolve latest modpack version from GitHub.");
        }

        return releaseInfo.TagName.Trim();
    }

    private string GetInstalledModpackVersion()
    {
        if (!File.Exists(_paths.ModpackVersionFilePath))
        {
            return string.Empty;
        }

        return File.ReadAllText(_paths.ModpackVersionFilePath).Trim();
    }
}
