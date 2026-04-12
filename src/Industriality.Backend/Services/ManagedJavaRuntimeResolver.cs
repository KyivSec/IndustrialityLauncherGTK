using System.Formats.Tar;
using System.IO.Compression;
using Industriality.Backend.Abstractions;
using Industriality.Backend.Models;

namespace Industriality.Backend.Services;

public sealed class ManagedJavaRuntimeResolver : IJavaRuntimeResolver
{
    private LauncherSettings _settings;
    private readonly LauncherPaths _paths;
    private string? _cachedJavaPath;

    public ManagedJavaRuntimeResolver(LauncherSettings settings, LauncherPaths paths)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _settings.Normalize();
    }

    public int JavaFeatureVersion => _settings.JavaFeatureVersion;

    public void UpdateSettings(LauncherSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Normalize();
        _cachedJavaPath = null;
    }

    public Task<string> ResolveJavaExecutablePathAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return ResolveManagedRuntimeAsync(progress, cancellationToken);
    }

    private async Task<string> ResolveManagedRuntimeAsync(
        IProgress<LauncherProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_cachedJavaPath) && File.Exists(_cachedJavaPath))
        {
            return _cachedJavaPath;
        }

        _paths.EnsureDirectories();

        var existingPath = JavaExecutableLocator.FindJavaExecutable(_paths.ManagedJavaVersionDirectory);
        if (!string.IsNullOrWhiteSpace(existingPath))
        {
            LauncherShared.ReportProgress(progress, "Java", "Using existing launcher-managed Java runtime.", 15);
            _cachedJavaPath = existingPath;
            return existingPath;
        }

        if (Directory.Exists(_paths.ManagedJavaVersionDirectory))
        {
            Directory.Delete(_paths.ManagedJavaVersionDirectory, recursive: true);
        }

        var temporaryDirectory = _paths.ManagedJavaVersionDirectory + ".tmp";
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }

        Directory.CreateDirectory(temporaryDirectory);

        if (File.Exists(_paths.ManagedJavaArchivePath))
        {
            File.Delete(_paths.ManagedJavaArchivePath);
        }

        var downloadUrl = AdoptiumUrlBuilder.BuildForCurrentPlatform(_settings.JavaFeatureVersion);
        LauncherShared.ReportProgress(progress, "Java", "Downloading managed Temurin runtime.", 8);

        try
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalLength = response.Content.Headers.ContentLength;

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var output = File.Create(_paths.ManagedJavaArchivePath))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    totalRead += read;

                    if (totalLength.HasValue && totalLength.Value > 0)
                    {
                        var ratio = (double)totalRead / totalLength.Value;
                        var percent = 8d + (ratio * 62d);
                        LauncherShared.ReportProgress(progress, "Java", "Downloading managed Temurin runtime.", percent);
                    }
                }
            }

            LauncherShared.ReportProgress(progress, "Java", "Extracting managed Temurin runtime.", 72);

            if (OperatingSystem.IsWindows())
            {
                ZipFile.ExtractToDirectory(_paths.ManagedJavaArchivePath, temporaryDirectory, overwriteFiles: true);
            }
            else
            {
                await using var archiveStream = File.OpenRead(_paths.ManagedJavaArchivePath);
                await using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);
                TarFile.ExtractToDirectory(gzipStream, temporaryDirectory, overwriteFiles: true);
            }

            if (Directory.Exists(_paths.ManagedJavaVersionDirectory))
            {
                Directory.Delete(_paths.ManagedJavaVersionDirectory, recursive: true);
            }

            Directory.Move(temporaryDirectory, _paths.ManagedJavaVersionDirectory);

            if (File.Exists(_paths.ManagedJavaArchivePath))
            {
                File.Delete(_paths.ManagedJavaArchivePath);
            }

            var executablePath = JavaExecutableLocator.FindJavaExecutable(_paths.ManagedJavaVersionDirectory)
                ?? throw new FileNotFoundException(
                    "Managed Java runtime was downloaded, but no Java executable was found.",
                    _paths.ManagedJavaVersionDirectory);

            LauncherShared.ReportProgress(progress, "Java", "Managed Temurin runtime is ready.", 80);

            _cachedJavaPath = executablePath;
            return executablePath;
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }

            throw;
        }
    }
}
