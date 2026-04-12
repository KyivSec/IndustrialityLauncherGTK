using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Industriality.Backend.Abstractions;
using Industriality.Backend.Models;
using Industriality.Backend.Serialization;

namespace Industriality.Backend.Services;

internal sealed class PlayService
{
    private LauncherSettings _settings;
    private readonly LauncherPaths _paths;
    private readonly IJavaRuntimeResolver _javaRuntimeResolver;
    private readonly object _processSync = new();
    private Process? _runningProcess;

    public PlayService(LauncherSettings settings, LauncherPaths paths, IJavaRuntimeResolver javaRuntimeResolver)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _javaRuntimeResolver = javaRuntimeResolver ?? throw new ArgumentNullException(nameof(javaRuntimeResolver));
    }

    public void UpdateSettings(LauncherSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task PlayAsync(string versionId, CancellationToken cancellationToken)
    {
        if (IsGameRunning())
        {
            return;
        }

        var javaPath = await _javaRuntimeResolver.ResolveJavaExecutablePathAsync(
            progress: null,
            cancellationToken).ConfigureAwait(false);

        var runtime = await ResolveRuntimeMetadataAsync(versionId, cancellationToken).ConfigureAwait(false);
        var startInfo = BuildStartInfo(javaPath, runtime, versionId);

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        process.Exited += (_, _) =>
        {
            lock (_processSync)
            {
                if (ReferenceEquals(_runningProcess, process))
                {
                    _runningProcess = null;
                }
            }

            process.Dispose();
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Minecraft process failed to start.");
        }

        lock (_processSync)
        {
            _runningProcess = process;
        }
    }

    public bool IsGameRunning()
    {
        lock (_processSync)
        {
            return _runningProcess is { HasExited: false };
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Process? process;

        lock (_processSync)
        {
            process = _runningProcess;
            _runningProcess = null;
        }

        if (process is null || process.HasExited)
        {
            return Task.CompletedTask;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }

        return Task.CompletedTask;
    }

    private async Task<LauncherRuntimeMetadata> ResolveRuntimeMetadataAsync(string versionId, CancellationToken cancellationToken)
    {
        if (File.Exists(_paths.RuntimeMetadataPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_paths.RuntimeMetadataPath, cancellationToken).ConfigureAwait(false);
                var metadata = JsonSerializer.Deserialize(json, BackendJsonContext.Default.LauncherRuntimeMetadata);
                if (metadata is not null && !string.IsNullOrWhiteSpace(metadata.MainClass) && metadata.ClasspathEntries.Length > 0)
                {
                    return metadata;
                }
            }
            catch
            {
            }
        }

        var fallback = BuildRuntimeMetadataFromInstalledVersion(versionId);
        if (fallback is null)
        {
            throw new InvalidOperationException(
                "Could not resolve launch runtime metadata. Reinstall the modpack to regenerate runtime files.");
        }

        return fallback;
    }

    private LauncherRuntimeMetadata? BuildRuntimeMetadataFromInstalledVersion(string versionId)
    {
        var jsonPath = Path.Combine(_paths.VersionsDirectory, versionId, versionId + ".json");
        if (!File.Exists(jsonPath))
        {
            var resolvedVersion = VersionResolver.TryResolveInstalledVersionIdFromDisk(_settings, _paths);
            if (string.IsNullOrWhiteSpace(resolvedVersion))
            {
                return null;
            }

            versionId = resolvedVersion;
            jsonPath = Path.Combine(_paths.VersionsDirectory, versionId, versionId + ".json");
            if (!File.Exists(jsonPath))
            {
                return null;
            }
        }

        using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = document.RootElement;

        var classpathEntries = new List<string>();
        if (root.TryGetProperty("libraries", out var librariesElement) && librariesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var library in librariesElement.EnumerateArray())
            {
                if (!library.TryGetProperty("downloads", out var downloadsElement) ||
                    !downloadsElement.TryGetProperty("artifact", out var artifactElement))
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
                    classpathEntries.Add(fullPath);
                }
            }
        }

        var versionJarPath = Path.ChangeExtension(jsonPath, ".jar");
        if (File.Exists(versionJarPath))
        {
            classpathEntries.Add(versionJarPath);
        }

        if (classpathEntries.Count == 0)
        {
            return null;
        }

        var assetIndexId = root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.String
            ? assetsElement.GetString() ?? string.Empty
            : string.Empty;

        return new LauncherRuntimeMetadata
        {
            VersionId = versionId,
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

    private ProcessStartInfo BuildStartInfo(string javaPath, LauncherRuntimeMetadata runtime, string fallbackVersionId)
    {
        var classpathEntries = runtime.ClasspathEntries
            .Where(path => !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (classpathEntries.Length == 0)
        {
            throw new InvalidOperationException("No valid classpath entries were found for launch.");
        }

        var classpathText = string.Join(Path.PathSeparator, classpathEntries);
        var resolvedVersionId = string.IsNullOrWhiteSpace(runtime.VersionId) ? fallbackVersionId : runtime.VersionId;
        var assetsDirectory = string.IsNullOrWhiteSpace(runtime.AssetsDirectory) ? _paths.AssetsDirectory : runtime.AssetsDirectory;
        var nativesDirectory = string.IsNullOrWhiteSpace(runtime.NativesDirectory) ? _paths.NativesDirectory : runtime.NativesDirectory;
        var mainClass = string.IsNullOrWhiteSpace(runtime.MainClass)
            ? throw new InvalidOperationException("Runtime metadata does not contain a valid main class.")
            : runtime.MainClass;

        var tokenMap = BuildTokenMap(runtime, resolvedVersionId, classpathEntries, classpathText, assetsDirectory, nativesDirectory, mainClass);
        var startInfo = new ProcessStartInfo
        {
            FileName = javaPath,
            WorkingDirectory = _paths.GameDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        startInfo.ArgumentList.Add($"-Xms{_settings.MinRamMb}m");
        startInfo.ArgumentList.Add($"-Xmx{_settings.MaxRamMb}m");

        if (Directory.Exists(nativesDirectory))
        {
            startInfo.ArgumentList.Add("-Djava.library.path=" + nativesDirectory);
        }

        foreach (var argument in ResolveArguments(runtime.ExtraJvmArguments, tokenMap))
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("-cp");
        startInfo.ArgumentList.Add(classpathText);
        startInfo.ArgumentList.Add(mainClass);

        var gameArguments = runtime.ExtraGameArguments.Length > 0
            ? ResolveArguments(runtime.ExtraGameArguments, tokenMap).ToArray()
            : BuildFallbackGameArguments(runtime, tokenMap);

        foreach (var argument in gameArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        SetJavaEnvironment(startInfo, javaPath);

        return startInfo;
    }

    private Dictionary<string, string> BuildTokenMap(
        LauncherRuntimeMetadata runtime,
        string versionId,
        IReadOnlyList<string> classpathEntries,
        string classpathText,
        string assetsDirectory,
        string nativesDirectory,
        string mainClass)
    {
        var username = string.IsNullOrWhiteSpace(_settings.PlayerName) ? "Player" : _settings.PlayerName.Trim();
        var playerUuid = BuildOfflineUuid(username);
        var libraryDirectory = TryResolveLibraryDirectory(classpathEntries);

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["${auth_player_name}"] = username,
            ["${version_name}"] = versionId,
            ["${game_directory}"] = _paths.GameDirectory,
            ["${game_assets}"] = assetsDirectory,
            ["${assets_root}"] = assetsDirectory,
            ["${assets_index_name}"] = runtime.AssetIndexId ?? string.Empty,
            ["${auth_uuid}"] = playerUuid,
            ["${auth_access_token}"] = "0",
            ["${auth_session}"] = "0",
            ["${user_type}"] = "legacy",
            ["${version_type}"] = "release",
            ["${natives_directory}"] = nativesDirectory,
            ["${launcher_name}"] = "IndustrialityLauncher",
            ["${launcher_version}"] = "1.0.0",
            ["${classpath}"] = classpathText,
            ["${classpath_separator}"] = Path.PathSeparator.ToString(),
            ["${library_directory}"] = libraryDirectory,
            ["${user_properties}"] = "{}",
            ["${clientid}"] = string.Empty,
            ["${auth_xuid}"] = string.Empty,
            ["${resolution_width}"] = string.Empty,
            ["${resolution_height}"] = string.Empty,
            ["${main_class}"] = mainClass
        };
    }

    private string[] BuildFallbackGameArguments(LauncherRuntimeMetadata runtime, IReadOnlyDictionary<string, string> tokenMap)
    {
        var result = new List<string>
        {
            "--username",
            tokenMap["${auth_player_name}"],
            "--version",
            tokenMap["${version_name}"],
            "--gameDir",
            tokenMap["${game_directory}"],
            "--uuid",
            tokenMap["${auth_uuid}"],
            "--accessToken",
            tokenMap["${auth_access_token}"],
            "--userType",
            tokenMap["${user_type}"],
            "--versionType",
            tokenMap["${version_type}"],
            "--userProperties",
            tokenMap["${user_properties}"]
        };

        if (!string.IsNullOrWhiteSpace(runtime.AssetsDirectory))
        {
            result.Add("--assetsDir");
            result.Add(runtime.AssetsDirectory);
        }

        if (!string.IsNullOrWhiteSpace(runtime.AssetIndexId))
        {
            result.Add("--assetIndex");
            result.Add(runtime.AssetIndexId);
        }

        return result.ToArray();
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

        var result = new List<string>();
        foreach (var entry in kindElement.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                var value = entry.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
        }

        return result.ToArray();
    }

    private static string[] ExtractGameArguments(JsonElement root)
    {
        var args = ExtractArguments(root, "game");
        if (args.Length > 0)
        {
            return args;
        }

        if (root.TryGetProperty("minecraftArguments", out var minecraftArgsElement) &&
            minecraftArgsElement.ValueKind == JsonValueKind.String)
        {
            return minecraftArgsElement
                .GetString()!
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return [];
    }

    private static IEnumerable<string> ResolveArguments(IEnumerable<string>? sourceArguments, IReadOnlyDictionary<string, string> tokenMap)
    {
        if (sourceArguments is null)
        {
            yield break;
        }

        foreach (var argument in sourceArguments)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            var expanded = NormalizeJvmArgument(ExpandTokens(argument, tokenMap)).Trim();
            if (expanded.Length == 0)
            {
                continue;
            }

            foreach (var token in SplitCommandLineArguments(expanded))
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    yield return token;
                }
            }
        }
    }

    private static string ExpandTokens(string value, IReadOnlyDictionary<string, string> tokenMap)
    {
        var result = value;
        foreach (var pair in tokenMap)
        {
            result = result.Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string NormalizeJvmArgument(string value)
    {
        return Regex.Replace(value, @"^(-D[^=\s]+)=\s+(.+?)\s*$", "$1=$2");
    }

    private static IEnumerable<string> SplitCommandLineArguments(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var builder = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                continue;
            }

            builder.Append(character);
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static string TryResolveLibraryDirectory(IReadOnlyList<string> classpathEntries)
    {
        foreach (var entry in classpathEntries)
        {
            var directoryPath = File.Exists(entry) ? Path.GetDirectoryName(entry) : entry;
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                continue;
            }

            var current = new DirectoryInfo(directoryPath);
            while (current is not null)
            {
                if (string.Equals(current.Name, "libraries", StringComparison.OrdinalIgnoreCase))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        return string.Empty;
    }

    private static string BuildOfflineUuid(string username)
    {
        var input = Encoding.UTF8.GetBytes("OfflinePlayer:" + username);
        var hash = MD5.HashData(input);

        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"{hex[..8]}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex.Substring(20, 12)}";
    }

    private static void SetJavaEnvironment(ProcessStartInfo startInfo, string javaPath)
    {
        var javaBinDirectory = Path.GetDirectoryName(javaPath)
            ?? throw new InvalidOperationException("Could not resolve Java bin directory.");
        var javaHomeDirectory = Directory.GetParent(javaBinDirectory)?.FullName
            ?? throw new InvalidOperationException("Could not resolve Java home directory.");

        startInfo.Environment["JAVA_HOME"] = javaHomeDirectory;

        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var existingPath = startInfo.Environment.TryGetValue("PATH", out var currentPath)
            ? currentPath ?? string.Empty
            : string.Empty;

        if (!existingPath.Split(separator, StringSplitOptions.RemoveEmptyEntries)
            .Any(value => string.Equals(value.Trim(), javaBinDirectory, StringComparison.OrdinalIgnoreCase)))
        {
            startInfo.Environment["PATH"] = string.IsNullOrWhiteSpace(existingPath)
                ? javaBinDirectory
                : javaBinDirectory + separator + existingPath;
        }
    }
}
