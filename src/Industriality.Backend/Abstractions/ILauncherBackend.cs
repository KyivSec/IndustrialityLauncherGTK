using Industriality.Backend.Models;

namespace Industriality.Backend.Abstractions;

public interface ILauncherBackend
{
    string RootDirectory { get; }
    string RuntimeDirectory { get; }
    string GameDirectory { get; }
    string SettingsFilePath { get; }
    string ModpackZipPath { get; }
    string ModpackVersionFilePath { get; }

    LauncherSettings GetSettings();
    void UpdateSettings(Action<LauncherSettings> updateAction);

    bool IsInstalled();
    string? GetInstalledVersionId();
    Task<InstallResult> InstallAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default);
    Task<bool> UpdateModpackAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default);
    Task PlayAsync(CancellationToken cancellationToken = default);
    void OpenRootFolder();
    void DeleteModpack();
    Task<ModpackUpdateInfo> GetModpackUpdateInfoAsync(CancellationToken cancellationToken = default);
}
