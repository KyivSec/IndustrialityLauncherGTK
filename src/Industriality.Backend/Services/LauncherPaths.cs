using System.Globalization;
using Industriality.Backend.Models;

namespace Industriality.Backend.Services;

public sealed class LauncherPaths
{
    private LauncherSettings _settings;

    public LauncherPaths(LauncherSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Normalize();
    }

    public string RootDirectory => _settings.RootDirectory;
    public string RuntimeDirectory => Path.Combine(RootDirectory, "runtimes");
    public string GameDirectory => Path.Combine(RootDirectory, "minecraft");
    public string ManagedJavaDirectory => Path.Combine(RuntimeDirectory, "java");
    public string ManagedJavaVersionDirectory =>
        Path.Combine(ManagedJavaDirectory, _settings.GetJavaRuntimeKey());
    public string ManagedJavaArchivePath
    {
        get
        {
            var extension = OperatingSystem.IsWindows() ? "zip" : "tar.gz";
            return Path.Combine(
                RuntimeDirectory,
                $"jdk-{_settings.JavaFeatureVersion.ToString(CultureInfo.InvariantCulture)}.{extension}");
        }
    }
    public string VersionsDirectory => Path.Combine(GameDirectory, "versions");
    public string LibrariesDirectory => Path.Combine(GameDirectory, "libraries");
    public string AssetsDirectory => Path.Combine(GameDirectory, "assets");
    public string AssetIndexesDirectory => Path.Combine(AssetsDirectory, "indexes");
    public string AssetObjectsDirectory => Path.Combine(AssetsDirectory, "objects");
    public string NativesDirectory => Path.Combine(GameDirectory, "natives");
    public string RuntimeMetadataPath => Path.Combine(GameDirectory, ".industriality", "runtime.json");
    public string NeoForgeInstallerPath => Path.Combine(
        RuntimeDirectory,
        $"neoforge-{_settings.NeoForgeVersion}-installer.jar");

    public string SettingsFilePath => Path.Combine(RootDirectory, "launcher-settings.json");
    public string ModpackZipPath => Path.Combine(RootDirectory, "Industriality.NeoForge.zip");
    public string ModpackVersionFilePath => Path.Combine(RootDirectory, "modpack-version.txt");

    public void UpdateSettings(LauncherSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Normalize();
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(RuntimeDirectory);
        Directory.CreateDirectory(GameDirectory);
        Directory.CreateDirectory(ManagedJavaDirectory);
    }
}
