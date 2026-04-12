using Industriality.Backend.Models;
using Industriality.UI.Gtk.Models;

namespace Industriality.UI.Gtk.Abstractions;

public interface IMainWindowActions
{
    Task<UiActionResult<UiSettingsSnapshot>> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task<UiActionResult> SaveSettingsAsync(UiSettingsSnapshot settings, CancellationToken cancellationToken = default);
    Task<UiActionResult<UiStatusSnapshot>> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> IsGameRunningAsync(CancellationToken cancellationToken = default);

    Task<UiActionResult> InstallAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default);
    Task<UiActionResult> UpdateAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default);
    Task<UiActionResult> PlayAsync(CancellationToken cancellationToken = default);
    Task<UiActionResult> StopAsync(CancellationToken cancellationToken = default);
    Task<UiActionResult> OpenRootFolderAsync(CancellationToken cancellationToken = default);
    Task<UiActionResult> DeleteModpackAsync(CancellationToken cancellationToken = default);
}
