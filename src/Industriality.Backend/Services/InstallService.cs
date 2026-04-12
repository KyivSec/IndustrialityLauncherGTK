using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;
using Industriality.Backend.Models;
using Industriality.Backend.Serialization;

namespace Industriality.Backend.Services;

internal sealed class InstallService
{
    private const int MaxParallelLibraryDownloads = 8;
    private const int MaxParallelAssetDownloads = 16;
    private const string VanillaManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

    private LauncherSettings _settings;
    private readonly LauncherPaths _paths;

    public InstallService(LauncherSettings settings, LauncherPaths paths)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public void UpdateSettings(LauncherSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task InstallVanillaAndNeoForgeAsync(
        string javaPath,
        IProgress<LauncherProgress>? progress,
        CancellationToken cancellationToken)
    {
        _paths.EnsureDirectories();

        LauncherShared.ReportProgress(progress, "Vanilla", "Installing vanilla Minecraft runtime.", 60);
        var vanillaRuntime = await DownloadVanillaRuntimeAsync(progress, cancellationToken).ConfigureAwait(false);

        LauncherShared.ReportProgress(progress, "NeoForge", "Installing NeoForge.", 82);
        var finalRuntime = await InstallNeoForgeRuntimeAsync(
            javaPath,
            vanillaRuntime,
            progress,
            cancellationToken).ConfigureAwait(false);

        await SaveRuntimeMetadataAsync(finalRuntime, cancellationToken).ConfigureAwait(false);
    }

    public string VerifyInstalledVersion()
    {
        var runtimeMetadata = TryLoadRuntimeMetadata();
        if (runtimeMetadata is not null && !string.IsNullOrWhiteSpace(runtimeMetadata.VersionId))
        {
            return runtimeMetadata.VersionId;
        }

        var versionId = VersionResolver.TryResolveInstalledVersionIdFromDisk(_settings, _paths);
        if (!string.IsNullOrWhiteSpace(versionId))
        {
            return versionId;
        }

        throw new DirectoryNotFoundException(
            "NeoForge installer finished, but expected version files were not found in: " +
            _paths.VersionsDirectory);
    }

    private async Task<LauncherRuntimeMetadata> DownloadVanillaRuntimeAsync(
        IProgress<LauncherProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.VersionsDirectory);
        Directory.CreateDirectory(_paths.LibrariesDirectory);
        Directory.CreateDirectory(_paths.AssetIndexesDirectory);
        Directory.CreateDirectory(_paths.AssetObjectsDirectory);

        if (Directory.Exists(_paths.NativesDirectory))
        {
            Directory.Delete(_paths.NativesDirectory, recursive: true);
        }

        Directory.CreateDirectory(_paths.NativesDirectory);

        using var httpClient = new HttpClient();

        var manifestJson = await httpClient.GetStringAsync(VanillaManifestUrl, cancellationToken).ConfigureAwait(false);
        using var manifestDocument = JsonDocument.Parse(manifestJson);

        var versionNode = manifestDocument.RootElement
            .GetProperty("versions")
            .EnumerateArray()
            .FirstOrDefault(item =>
                string.Equals(item.GetProperty("id").GetString(), _settings.MinecraftVersion, StringComparison.OrdinalIgnoreCase));

        if (versionNode.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("Could not resolve Minecraft version metadata.");
        }

        var versionUrl = versionNode.GetProperty("url").GetString();
        if (string.IsNullOrWhiteSpace(versionUrl))
        {
            throw new InvalidOperationException("Minecraft version metadata URL was missing.");
        }

        var versionJson = await httpClient.GetStringAsync(versionUrl, cancellationToken).ConfigureAwait(false);
        using var versionDocument = JsonDocument.Parse(versionJson);
        var root = versionDocument.RootElement;

        var vanillaVersionDirectory = Path.Combine(_paths.VersionsDirectory, _settings.MinecraftVersion);
        Directory.CreateDirectory(vanillaVersionDirectory);

        var vanillaVersionJsonPath = Path.Combine(vanillaVersionDirectory, _settings.MinecraftVersion + ".json");
        await File.WriteAllTextAsync(vanillaVersionJsonPath, versionJson, cancellationToken).ConfigureAwait(false);

        var clientDownload = root.GetProperty("downloads").GetProperty("client");
        var clientUrl = clientDownload.GetProperty("url").GetString()
            ?? throw new InvalidOperationException("Minecraft client download URL was missing.");
        var clientSha1 = clientDownload.TryGetProperty("sha1", out var clientShaElement)
            ? clientShaElement.GetString()
            : null;

        var clientJarPath = Path.Combine(vanillaVersionDirectory, _settings.MinecraftVersion + ".jar");
        await DownloadFileAsync(httpClient, clientUrl, clientJarPath, clientSha1, cancellationToken).ConfigureAwait(false);

        var classpathEntries = new List<string>();
        var nativeArchives = new List<string>();

        if (root.TryGetProperty("libraries", out var librariesElement) && librariesElement.ValueKind == JsonValueKind.Array)
        {
            await DownloadLibrariesAsync(
                librariesElement,
                classpathEntries,
                nativeArchives,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var nativeArchive in nativeArchives.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ExtractNativeArchive(nativeArchive, _paths.NativesDirectory);
        }

        classpathEntries.Add(clientJarPath);

        string assetIndexId = string.Empty;
        if (root.TryGetProperty("assetIndex", out var assetIndexElement))
        {
            assetIndexId = assetIndexElement.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
                ? idElement.GetString() ?? string.Empty
                : string.Empty;
            var assetIndexUrl = assetIndexElement.TryGetProperty("url", out var urlElement) && urlElement.ValueKind == JsonValueKind.String
                ? urlElement.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(assetIndexId) && !string.IsNullOrWhiteSpace(assetIndexUrl))
            {
                var assetIndexPath = Path.Combine(_paths.AssetIndexesDirectory, assetIndexId + ".json");
                var assetIndexSha1 = assetIndexElement.TryGetProperty("sha1", out var indexShaElement)
                    ? indexShaElement.GetString()
                    : null;

                await DownloadFileAsync(httpClient, assetIndexUrl, assetIndexPath, assetIndexSha1, cancellationToken)
                    .ConfigureAwait(false);

                var assetIndexJson = await File.ReadAllTextAsync(assetIndexPath, cancellationToken).ConfigureAwait(false);
                using var assetIndexDocument = JsonDocument.Parse(assetIndexJson);

                if (assetIndexDocument.RootElement.TryGetProperty("objects", out var objectsElement))
                {
                    await DownloadAssetsAsync(httpClient, objectsElement, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        LauncherShared.ReportProgress(progress, "Vanilla", "Vanilla runtime installed.", 78);

        return new LauncherRuntimeMetadata
        {
            VersionId = _settings.MinecraftVersion,
            MainClass = root.TryGetProperty("mainClass", out var mainClassElement) && mainClassElement.ValueKind == JsonValueKind.String
                ? mainClassElement.GetString() ?? string.Empty
                : string.Empty,
            ClasspathEntries = classpathEntries
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            AssetsDirectory = _paths.AssetsDirectory,
            AssetIndexId = assetIndexId,
            NativesDirectory = _paths.NativesDirectory,
            ExtraJvmArguments = ExtractArguments(root, "jvm"),
            ExtraGameArguments = ExtractGameArguments(root)
        };
    }

    private async Task<LauncherRuntimeMetadata> InstallNeoForgeRuntimeAsync(
        string javaPath,
        LauncherRuntimeMetadata vanillaRuntime,
        IProgress<LauncherProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();

        var installerUrl =
            $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{_settings.NeoForgeVersion}/neoforge-{_settings.NeoForgeVersion}-installer.jar";
        await DownloadFileAsync(httpClient, installerUrl, _paths.NeoForgeInstallerPath, sha1: null, cancellationToken)
            .ConfigureAwait(false);

        EnsureLauncherProfiles(_paths.GameDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = javaPath,
            WorkingDirectory = _paths.GameDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("-jar");
        startInfo.ArgumentList.Add(_paths.NeoForgeInstallerPath);
        startInfo.ArgumentList.Add("--install-client");
        startInfo.ArgumentList.Add(".");

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();
        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "NeoForge installer failed." + Environment.NewLine +
                "ExitCode: " + process.ExitCode + Environment.NewLine +
                "StdOut:" + Environment.NewLine + stdOut + Environment.NewLine +
                "StdErr:" + Environment.NewLine + stdErr);
        }

        var versionJsonPath = ResolveInstalledNeoForgeVersionJsonPath()
            ?? throw new InvalidOperationException("Could not locate installed NeoForge version json.");
        var versionId = Path.GetFileNameWithoutExtension(versionJsonPath);

        var loaderJson = await File.ReadAllTextAsync(versionJsonPath, cancellationToken).ConfigureAwait(false);
        using var loaderDocument = JsonDocument.Parse(loaderJson);
        var loaderRoot = loaderDocument.RootElement;

        var loaderClasspath = ResolveLoaderClasspath(loaderRoot);
        var versionJarPath = Path.ChangeExtension(versionJsonPath, ".jar");
        if (File.Exists(versionJarPath))
        {
            loaderClasspath.Add(versionJarPath);
        }

        var combinedClasspath = new List<string>();
        combinedClasspath.AddRange(loaderClasspath);
        foreach (var path in vanillaRuntime.ClasspathEntries)
        {
            if (!combinedClasspath.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                combinedClasspath.Add(path);
            }
        }

        LauncherShared.ReportProgress(progress, "NeoForge", "NeoForge runtime installed.", 96);

        return new LauncherRuntimeMetadata
        {
            VersionId = versionId,
            MainClass = loaderRoot.TryGetProperty("mainClass", out var mainClassElement) && mainClassElement.ValueKind == JsonValueKind.String
                ? mainClassElement.GetString() ?? vanillaRuntime.MainClass
                : vanillaRuntime.MainClass,
            ClasspathEntries = combinedClasspath
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            AssetsDirectory = vanillaRuntime.AssetsDirectory,
            AssetIndexId = vanillaRuntime.AssetIndexId,
            NativesDirectory = vanillaRuntime.NativesDirectory,
            ExtraJvmArguments = MergePreservingOrder(vanillaRuntime.ExtraJvmArguments, ExtractArguments(loaderRoot, "jvm")),
            ExtraGameArguments = MergePreservingOrder(vanillaRuntime.ExtraGameArguments, ExtractGameArguments(loaderRoot))
        };
    }

    private LauncherRuntimeMetadata? TryLoadRuntimeMetadata()
    {
        if (!File.Exists(_paths.RuntimeMetadataPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_paths.RuntimeMetadataPath);
            return JsonSerializer.Deserialize(json, BackendJsonContext.Default.LauncherRuntimeMetadata);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveRuntimeMetadataAsync(LauncherRuntimeMetadata metadata, CancellationToken cancellationToken)
    {
        var metadataDirectory = Path.GetDirectoryName(_paths.RuntimeMetadataPath);
        if (!string.IsNullOrWhiteSpace(metadataDirectory))
        {
            Directory.CreateDirectory(metadataDirectory);
        }

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = BackendJsonContext.Default
        };
        var jsonContext = new BackendJsonContext(jsonOptions);
        var json = JsonSerializer.Serialize(metadata, jsonContext.LauncherRuntimeMetadata);
        await File.WriteAllTextAsync(_paths.RuntimeMetadataPath, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task DownloadLibrariesAsync(
        JsonElement librariesElement,
        List<string> classpathEntries,
        List<string> nativeArchives,
        CancellationToken cancellationToken)
    {
        var workItems = new List<LibraryDownloadWorkItem>();

        foreach (var library in librariesElement.EnumerateArray())
        {
            if (!IsLibraryAllowed(library))
            {
                continue;
            }

            if (!library.TryGetProperty("downloads", out var downloadsElement))
            {
                continue;
            }

            if (downloadsElement.TryGetProperty("artifact", out var artifactElement))
            {
                var relativePath = artifactElement.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
                var url = artifactElement.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
                var sha1 = artifactElement.TryGetProperty("sha1", out var shaElement) ? shaElement.GetString() : null;

                if (!string.IsNullOrWhiteSpace(relativePath) && !string.IsNullOrWhiteSpace(url))
                {
                    workItems.Add(new LibraryDownloadWorkItem
                    {
                        Url = url!,
                        DestinationPath = Path.Combine(_paths.LibrariesDirectory, relativePath!),
                        Sha1 = sha1,
                        AddToClasspath = true,
                        IsNativeArchive = false
                    });
                }
            }

            if (downloadsElement.TryGetProperty("classifiers", out var classifiersElement))
            {
                var nativeKey = GetNativeClassifierKey(library);
                if (!string.IsNullOrWhiteSpace(nativeKey) &&
                    classifiersElement.TryGetProperty(nativeKey, out var nativeElement))
                {
                    var nativeRelativePath = nativeElement.TryGetProperty("path", out var nativePathElement)
                        ? nativePathElement.GetString()
                        : null;
                    var nativeUrl = nativeElement.TryGetProperty("url", out var nativeUrlElement)
                        ? nativeUrlElement.GetString()
                        : null;
                    var nativeSha1 = nativeElement.TryGetProperty("sha1", out var nativeShaElement)
                        ? nativeShaElement.GetString()
                        : null;

                    if (!string.IsNullOrWhiteSpace(nativeRelativePath) && !string.IsNullOrWhiteSpace(nativeUrl))
                    {
                        workItems.Add(new LibraryDownloadWorkItem
                        {
                            Url = nativeUrl!,
                            DestinationPath = Path.Combine(_paths.LibrariesDirectory, nativeRelativePath!),
                            Sha1 = nativeSha1,
                            AddToClasspath = false,
                            IsNativeArchive = true
                        });
                    }
                }
            }
        }

        if (workItems.Count == 0)
        {
            return;
        }

        var downloadedClasspath = new System.Collections.Concurrent.ConcurrentBag<string>();
        var downloadedNatives = new System.Collections.Concurrent.ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            workItems,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxParallelLibraryDownloads,
                CancellationToken = cancellationToken
            },
            async (item, ct) =>
            {
                using var httpClient = new HttpClient();
                await DownloadFileAsync(httpClient, item.Url, item.DestinationPath, item.Sha1, ct).ConfigureAwait(false);

                if (item.AddToClasspath)
                {
                    downloadedClasspath.Add(item.DestinationPath);
                }

                if (item.IsNativeArchive)
                {
                    downloadedNatives.Add(item.DestinationPath);
                }
            }).ConfigureAwait(false);

        classpathEntries.AddRange(downloadedClasspath);
        nativeArchives.AddRange(downloadedNatives);
    }

    private async Task DownloadAssetsAsync(
        HttpClient httpClient,
        JsonElement objectsElement,
        CancellationToken cancellationToken)
    {
        var assetHashes = objectsElement
            .EnumerateObject()
            .Select(property => property.Value.TryGetProperty("hash", out var hashElement) ? hashElement.GetString() : null)
            .Where(hash => !string.IsNullOrWhiteSpace(hash) && hash!.Length >= 2)
            .Select(hash => hash!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await Parallel.ForEachAsync(
            assetHashes,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxParallelAssetDownloads,
                CancellationToken = cancellationToken
            },
            async (hash, ct) =>
            {
                var prefix = hash[..2];
                var destinationPath = Path.Combine(_paths.AssetObjectsDirectory, prefix, hash);
                var url = "https://resources.download.minecraft.net/" + prefix + "/" + hash;
                using var scopedClient = new HttpClient();
                await DownloadFileAsync(scopedClient, url, destinationPath, hash, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    private static async Task DownloadFileAsync(
        HttpClient httpClient,
        string url,
        string destinationPath,
        string? sha1,
        CancellationToken cancellationToken)
    {
        if (File.Exists(destinationPath) && !string.IsNullOrWhiteSpace(sha1))
        {
            var existingSha1 = await ComputeSha1Async(destinationPath, cancellationToken).ConfigureAwait(false);
            if (string.Equals(existingSha1, sha1, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var tempPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var input = await httpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false))
            await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(sha1))
            {
                var downloadedSha1 = await ComputeSha1Async(tempPath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(downloadedSha1, sha1, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Downloaded file hash mismatch.");
                }
            }

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(tempPath, destinationPath);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                }
            }

            throw;
        }
    }

    private static async Task<string> ComputeSha1Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha1 = SHA1.Create();
        var hash = await sha1.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsLibraryAllowed(JsonElement library)
    {
        if (!library.TryGetProperty("rules", out var rulesElement) || rulesElement.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        var allowed = false;

        foreach (var rule in rulesElement.EnumerateArray())
        {
            var applies = true;
            if (rule.TryGetProperty("os", out var osElement) &&
                osElement.TryGetProperty("name", out var osNameElement))
            {
                applies = IsCurrentOs(osNameElement.GetString());
            }

            if (!applies)
            {
                continue;
            }

            var action = rule.TryGetProperty("action", out var actionElement)
                ? actionElement.GetString()
                : null;
            if (string.Equals(action, "allow", StringComparison.OrdinalIgnoreCase))
            {
                allowed = true;
            }
            else if (string.Equals(action, "disallow", StringComparison.OrdinalIgnoreCase))
            {
                allowed = false;
            }
        }

        return allowed;
    }

    private static string? GetNativeClassifierKey(JsonElement library)
    {
        if (!library.TryGetProperty("natives", out var nativesElement))
        {
            return null;
        }

        if (OperatingSystem.IsWindows() &&
            nativesElement.TryGetProperty("windows", out var windowsElement) &&
            windowsElement.ValueKind == JsonValueKind.String)
        {
            return windowsElement.GetString();
        }

        if (OperatingSystem.IsLinux() &&
            nativesElement.TryGetProperty("linux", out var linuxElement) &&
            linuxElement.ValueKind == JsonValueKind.String)
        {
            return linuxElement.GetString();
        }

        if (OperatingSystem.IsMacOS() &&
            nativesElement.TryGetProperty("osx", out var osxElement) &&
            osxElement.ValueKind == JsonValueKind.String)
        {
            return osxElement.GetString();
        }

        return null;
    }

    private static bool IsCurrentOs(string? osName)
    {
        return (OperatingSystem.IsWindows() && string.Equals(osName, "windows", StringComparison.OrdinalIgnoreCase)) ||
               (OperatingSystem.IsLinux() && string.Equals(osName, "linux", StringComparison.OrdinalIgnoreCase)) ||
               (OperatingSystem.IsMacOS() && string.Equals(osName, "osx", StringComparison.OrdinalIgnoreCase));
    }

    private static void ExtractNativeArchive(string archivePath, string destinationDirectory)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            var normalizedPath = entry.FullName.Replace('\\', '/');
            if (normalizedPath.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destinationPath = Path.Combine(destinationDirectory, entry.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private string? ResolveInstalledNeoForgeVersionJsonPath()
    {
        if (!Directory.Exists(_paths.VersionsDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(_paths.VersionsDirectory, "*.json", SearchOption.AllDirectories)
            .Select(path =>
            {
                var id = Path.GetFileNameWithoutExtension(path);
                var score = 0;
                if (id.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                {
                    score += 100;
                }

                if (id.Contains(_settings.NeoForgeVersion, StringComparison.OrdinalIgnoreCase))
                {
                    score += 20;
                }

                return new
                {
                    Path = path,
                    Score = score,
                    LastWrite = File.GetLastWriteTimeUtc(path)
                };
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.LastWrite)
            .Select(item => item.Path)
            .FirstOrDefault();
    }

    private List<string> ResolveLoaderClasspath(JsonElement versionRoot)
    {
        var result = new List<string>();
        if (!versionRoot.TryGetProperty("libraries", out var librariesElement) || librariesElement.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var library in librariesElement.EnumerateArray())
        {
            if (!library.TryGetProperty("downloads", out var downloadsElement))
            {
                continue;
            }

            if (!downloadsElement.TryGetProperty("artifact", out var artifactElement))
            {
                continue;
            }

            var relativePath = artifactElement.TryGetProperty("path", out var pathElement) && pathElement.ValueKind == JsonValueKind.String
                ? pathElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var fullPath = Path.Combine(_paths.LibrariesDirectory, relativePath);
            if (File.Exists(fullPath))
            {
                result.Add(fullPath);
            }
        }

        return result;
    }

    private static string[] ExtractArguments(JsonElement root, string kind)
    {
        if (!root.TryGetProperty("arguments", out var argumentsElement) || argumentsElement.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (!argumentsElement.TryGetProperty(kind, out var kindElement) || kindElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var entry in kindElement.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                var value = entry.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }
        }

        return values.ToArray();
    }

    private static string[] ExtractGameArguments(JsonElement root)
    {
        var args = ExtractArguments(root, "game");
        if (args.Length > 0)
        {
            return args;
        }

        if (root.TryGetProperty("minecraftArguments", out var mcArgsElement) && mcArgsElement.ValueKind == JsonValueKind.String)
        {
            return mcArgsElement
                .GetString()!
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return [];
    }

    private static string[] MergePreservingOrder(IEnumerable<string>? existing, IEnumerable<string>? incoming)
    {
        var result = new List<string>();

        if (existing is not null)
        {
            foreach (var value in existing)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
        }

        if (incoming is not null)
        {
            foreach (var value in incoming)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
        }

        return result.ToArray();
    }

    private static void EnsureLauncherProfiles(string gameDirectory)
    {
        var launcherProfilesPath = Path.Combine(gameDirectory, "launcher_profiles.json");
        if (File.Exists(launcherProfilesPath))
        {
            return;
        }

        const string launcherProfilesJson =
            """
            {
              "profiles": {},
              "settings": {},
              "version": 3
            }
            """;

        File.WriteAllText(launcherProfilesPath, launcherProfilesJson);
    }

    private sealed class LibraryDownloadWorkItem
    {
        public string Url { get; init; } = string.Empty;
        public string DestinationPath { get; init; } = string.Empty;
        public string? Sha1 { get; init; }
        public bool AddToClasspath { get; init; }
        public bool IsNativeArchive { get; init; }
    }
}
