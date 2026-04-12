namespace Industriality.UI.Gtk.Models;

public sealed record UiStatusSnapshot(
    bool IsInstalled,
    string InstalledVersion,
    string LatestVersion,
    bool UpdateAvailable,
    bool IsRunning);
