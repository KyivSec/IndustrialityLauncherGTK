using Industriality.Backend.Abstractions;
using Industriality.Backend.Models;
using Industriality.Backend.Services;

namespace Industriality.Backend;

public sealed class LauncherBackend : ILauncherBackend
{
    private LauncherSettings _settings;
    private readonly LauncherPaths _paths;
    private readonly ManagedJavaRuntimeResolver _javaRuntimeResolver;
    private readonly InstallService _installService;
    private readonly ModpackService _modpackService;
    private readonly PlayService _playService;
    private string? _installedVersionId;

    public LauncherBackend()
        : this(LauncherSettings.CreateDefault())
    {
    }

    public LauncherBackend(LauncherSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Normalize();

        _paths = new LauncherPaths(_settings);
        _javaRuntimeResolver = new ManagedJavaRuntimeResolver(_settings, _paths);
        _installService = new InstallService(_settings, _paths);
        _modpackService = new ModpackService(_settings, _paths);
        _playService = new PlayService(_settings, _paths, _javaRuntimeResolver);

        _paths.EnsureDirectories();
    }

    public string RootDirectory => _paths.RootDirectory;
    public string RuntimeDirectory => _paths.RuntimeDirectory;
    public string GameDirectory => _paths.GameDirectory;
    public string SettingsFilePath => _paths.SettingsFilePath;
    public string ModpackZipPath => _paths.ModpackZipPath;
    public string ModpackVersionFilePath => _paths.ModpackVersionFilePath;

    public LauncherSettings GetSettings()
    {
        return _settings.Clone();
    }

    public void UpdateSettings(Action<LauncherSettings> updateAction)
    {
        ArgumentNullException.ThrowIfNull(updateAction);

        var updatedSettings = _settings.Clone();
        updateAction(updatedSettings);
        updatedSettings.Normalize();

        _settings = updatedSettings;
        _paths.UpdateSettings(_settings);
        _javaRuntimeResolver.UpdateSettings(_settings);
        _installService.UpdateSettings(_settings);
        _modpackService.UpdateSettings(_settings);
        _playService.UpdateSettings(_settings);

        _installedVersionId = null;
        _paths.EnsureDirectories();
    }

    public bool IsInstalled()
    {
        _installedVersionId = VersionResolver.TryResolveInstalledVersionIdFromDisk(_settings, _paths);
        return !string.IsNullOrWhiteSpace(_installedVersionId) && _modpackService.IsModpackContentInstalled();
    }

    public string? GetInstalledVersionId()
    {
        _installedVersionId = VersionResolver.TryResolveInstalledVersionIdFromDisk(_settings, _paths);
        return _installedVersionId;
    }

    public async Task<InstallResult> InstallAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();

        LauncherShared.ReportProgress(progress, "Preparing", "Creating launcher directories.", 2);

        var javaPath = await _javaRuntimeResolver.ResolveJavaExecutablePathAsync(progress, cancellationToken)
            .ConfigureAwait(false);
        await _installService.InstallVanillaAndNeoForgeAsync(javaPath, progress, cancellationToken).ConfigureAwait(false);
        await _modpackService.DownloadAndInstallModpackAsync(progress, cancellationToken).ConfigureAwait(false);

        LauncherShared.ReportProgress(progress, "Verifying", "Checking installed files.", 99);
        var versionId = _installService.VerifyInstalledVersion();
        _installedVersionId = versionId;

        LauncherShared.ReportProgress(progress, "Done", "Installation complete.", 100);

        return new InstallResult
        {
            RootDirectory = _paths.RootDirectory,
            GameDirectory = _paths.GameDirectory,
            JavaPath = javaPath,
            VersionId = versionId
        };
    }

    public async Task<bool> UpdateModpackAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();

        var updateInfo = await _modpackService.GetModpackUpdateInfoAsync(cancellationToken).ConfigureAwait(false);
        if (!updateInfo.UpdateAvailable)
        {
            LauncherShared.ReportProgress(progress, "Update", "Modpack is already up to date.", 100);
            return false;
        }

        await _modpackService.DownloadAndInstallModpackAsync(progress, cancellationToken).ConfigureAwait(false);
        LauncherShared.ReportProgress(progress, "Done", "Update complete.", 100);
        return true;
    }

    public async Task PlayAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();

        var versionId = _installedVersionId;
        if (string.IsNullOrWhiteSpace(versionId))
        {
            versionId = VersionResolver.TryResolveInstalledVersionIdFromDisk(_settings, _paths);
            if (string.IsNullOrWhiteSpace(versionId))
            {
                throw new InvalidOperationException("NeoForge is not installed. Run InstallAsync first.");
            }
        }

        _installedVersionId = versionId;
        await _playService.PlayAsync(versionId, cancellationToken).ConfigureAwait(false);
    }

    public void OpenRootFolder()
    {
        _paths.EnsureDirectories();
        LauncherShared.OpenFolderCrossPlatform(_paths.RootDirectory);
    }

    public void DeleteModpack()
    {
        if (Directory.Exists(_paths.GameDirectory))
        {
            Directory.Delete(_paths.GameDirectory, recursive: true);
        }

        Directory.CreateDirectory(_paths.GameDirectory);

        if (File.Exists(_paths.ModpackVersionFilePath))
        {
            File.Delete(_paths.ModpackVersionFilePath);
        }

        if (File.Exists(_paths.ModpackZipPath))
        {
            File.Delete(_paths.ModpackZipPath);
        }

        _installedVersionId = null;
    }

    public Task<ModpackUpdateInfo> GetModpackUpdateInfoAsync(CancellationToken cancellationToken = default)
    {
        return _modpackService.GetModpackUpdateInfoAsync(cancellationToken);
    }
}
