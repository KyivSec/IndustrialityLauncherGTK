using Industriality.Backend.Models;
using Industriality.UI.Gtk.Abstractions;
using Industriality.UI.Gtk.Models;

namespace Industriality.UI.Gtk.Actions;

public sealed class NoOpMainWindowActions : IMainWindowActions
{
    private const string Placeholder = "Action placeholder: backend wiring for UI is scheduled for a later milestone.";

    public Task<UiActionResult<UiSettingsSnapshot>> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UiActionResult<UiSettingsSnapshot>.Ok(
            new UiSettingsSnapshot("Player", 512, 4096),
            Placeholder));
    }

    public Task<UiActionResult> SaveSettingsAsync(UiSettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UiActionResult.Ok(Placeholder));
    }

    public Task<UiActionResult<UiStatusSnapshot>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UiActionResult<UiStatusSnapshot>.Ok(
            new UiStatusSnapshot(false, "Not installed", string.Empty, false, false),
            Placeholder));
    }

    public Task<bool> IsGameRunningAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<UiActionResult> InstallAsync(IProgress<LauncherProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UiActionResult.Ok(Placeholder));
    }

    public Task<UiActionResult> UpdateAsync(IProgress<LauncherProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UiActionResult.Ok(Placeholder));
    }

    public Task<UiActionResult> PlayAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UiActionResult.Ok(Placeholder));
    }

    public Task<UiActionResult> StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UiActionResult.Ok(Placeholder));
    }

    public Task<UiActionResult> OpenRootFolderAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UiActionResult.Ok(Placeholder));
    }

    public Task<UiActionResult> DeleteModpackAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UiActionResult.Ok(Placeholder));
    }
}
