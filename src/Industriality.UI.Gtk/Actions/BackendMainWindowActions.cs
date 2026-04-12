using Industriality.Backend.Abstractions;
using Industriality.Backend.Models;
using Industriality.UI.Gtk.Abstractions;
using Industriality.UI.Gtk.Models;

namespace Industriality.UI.Gtk.Actions;

public sealed class BackendMainWindowActions : IMainWindowActions
{
    private readonly ILauncherBackend _backend;
    private readonly ISettingsStore _settingsStore;

    public BackendMainWindowActions(ILauncherBackend backend, ISettingsStore settingsStore)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    }

    public async Task<UiActionResult<UiSettingsSnapshot>> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var loadedSettings = await _settingsStore
                .LoadAsync(_backend.SettingsFilePath, cancellationToken)
                .ConfigureAwait(false);

            ApplyUiSettingsToBackend(loadedSettings);
            var normalized = _backend.GetSettings();

            var snapshot = new UiSettingsSnapshot(
                normalized.PlayerName,
                normalized.MinRamMb,
                normalized.MaxRamMb);

            await _settingsStore
                .SaveAsync(_backend.SettingsFilePath, ToUiSettings(snapshot), cancellationToken)
                .ConfigureAwait(false);

            return UiActionResult<UiSettingsSnapshot>.Ok(snapshot, "Settings loaded.");
        }
        catch (Exception exception)
        {
            return UiActionResult<UiSettingsSnapshot>.Fail(
                "Failed to load launcher settings.",
                exception.ToString(),
                new UiSettingsSnapshot("Player", 512, 4096));
        }
    }

    public async Task<UiActionResult> SaveSettingsAsync(UiSettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            _backend.UpdateSettings(launcherSettings =>
            {
                launcherSettings.PlayerName = settings.Username;
                launcherSettings.MinRamMb = settings.MinRamMb;
                launcherSettings.MaxRamMb = settings.MaxRamMb;
            });

            var normalized = _backend.GetSettings();

            await _settingsStore
                .SaveAsync(
                    _backend.SettingsFilePath,
                    new UiSettings
                    {
                        Username = normalized.PlayerName,
                        MinRamMb = normalized.MinRamMb,
                        MaxRamMb = normalized.MaxRamMb
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return UiActionResult.Ok("Settings saved.");
        }
        catch (Exception exception)
        {
            return UiActionResult.Fail("Failed to save launcher settings.", exception.ToString());
        }
    }

    public async Task<UiActionResult<UiStatusSnapshot>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var isInstalled = _backend.IsInstalled();
            var isRunning = _backend.IsGameRunning();
            var installedVersion = ResolveInstalledVersion();
            var latestVersion = string.Empty;
            var updateAvailable = false;

            try
            {
                var info = await _backend.GetModpackUpdateInfoAsync(cancellationToken).ConfigureAwait(false);
                latestVersion = info.LatestVersion;
                updateAvailable = isInstalled && info.UpdateAvailable;

                if (!string.IsNullOrWhiteSpace(info.CurrentVersion))
                {
                    installedVersion = info.CurrentVersion;
                }
            }
            catch (Exception exception)
            {
                var fallbackSnapshot = new UiStatusSnapshot(
                    isInstalled,
                    installedVersion,
                    latestVersion,
                    updateAvailable,
                    isRunning);

                return UiActionResult<UiStatusSnapshot>.Fail(
                    "Status loaded, but update check failed.",
                    exception.ToString(),
                    fallbackSnapshot);
            }

            return UiActionResult<UiStatusSnapshot>.Ok(
                new UiStatusSnapshot(
                    isInstalled,
                    installedVersion,
                    latestVersion,
                    updateAvailable,
                    isRunning),
                "Status loaded.");
        }
        catch (Exception exception)
        {
            return UiActionResult<UiStatusSnapshot>.Fail(
                "Failed to load launcher status.",
                exception.ToString(),
                new UiStatusSnapshot(false, "Not installed", string.Empty, false, false));
        }
    }

    public Task<bool> IsGameRunningAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_backend.IsGameRunning());
    }

    public async Task<UiActionResult> InstallAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _backend
                .InstallAsync(progress, cancellationToken)
                .ConfigureAwait(false);

            return UiActionResult.Ok($"Installation completed. Version {result.VersionId} is ready.");
        }
        catch (OperationCanceledException)
        {
            return UiActionResult.Fail("Installation was canceled.");
        }
        catch (Exception exception)
        {
            return UiActionResult.Fail("Installation failed.", exception.ToString());
        }
    }

    public async Task<UiActionResult> UpdateAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _backend
                .UpdateModpackAsync(progress, cancellationToken)
                .ConfigureAwait(false);

            return updated
                ? UiActionResult.Ok("Update completed successfully.")
                : UiActionResult.Ok("Modpack is already up to date.");
        }
        catch (OperationCanceledException)
        {
            return UiActionResult.Fail("Update was canceled.");
        }
        catch (Exception exception)
        {
            return UiActionResult.Fail("Update failed.", exception.ToString());
        }
    }

    public async Task<UiActionResult> PlayAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_backend.IsInstalled())
            {
                return UiActionResult.Fail("Modpack is not installed. Please install before playing.");
            }

            await _backend.PlayAsync(cancellationToken).ConfigureAwait(false);
            return UiActionResult.Ok(string.Empty);
        }
        catch (OperationCanceledException)
        {
            return UiActionResult.Fail("Launch was canceled.");
        }
        catch (Exception exception)
        {
            return UiActionResult.Fail("Failed to start Minecraft.", exception.ToString());
        }
    }

    public async Task<UiActionResult> StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _backend.StopAsync(cancellationToken).ConfigureAwait(false);
            return UiActionResult.Ok("Game process stopped.");
        }
        catch (OperationCanceledException)
        {
            return UiActionResult.Fail("Stop was canceled.");
        }
        catch (Exception exception)
        {
            return UiActionResult.Fail("Failed to stop Minecraft.", exception.ToString());
        }
    }

    public Task<UiActionResult> OpenRootFolderAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _backend.OpenRootFolder();
            return Task.FromResult(UiActionResult.Ok("Launcher folder opened."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(UiActionResult.Fail("Failed to open launcher folder.", exception.ToString()));
        }
    }

    public Task<UiActionResult> DeleteModpackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _backend.DeleteModpack();
            return Task.FromResult(UiActionResult.Ok("Modpack deleted."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(UiActionResult.Fail("Failed to delete modpack.", exception.ToString()));
        }
    }

    private void ApplyUiSettingsToBackend(UiSettings uiSettings)
    {
        _backend.UpdateSettings(settings =>
        {
            settings.PlayerName = string.IsNullOrWhiteSpace(uiSettings.Username)
                ? settings.PlayerName
                : uiSettings.Username;
            settings.MinRamMb = uiSettings.MinRamMb;
            settings.MaxRamMb = uiSettings.MaxRamMb;
        });
    }

    private UiSettings ToUiSettings(UiSettingsSnapshot snapshot)
    {
        return new UiSettings
        {
            Username = snapshot.Username,
            MinRamMb = snapshot.MinRamMb,
            MaxRamMb = snapshot.MaxRamMb
        };
    }

    private string ResolveInstalledVersion()
    {
        var version = _backend.GetInstalledVersionId();
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        return _backend.IsInstalled() ? "Installed" : "Not installed";
    }
}
