using Industriality.UI.Gtk.Abstractions;

namespace Industriality.UI.Gtk.Actions;

public sealed class NoOpMainWindowActions : IMainWindowActions
{
    private const string Placeholder = "Action placeholder: backend wiring for UI is scheduled for a later milestone.";

    public string OnInstallRequested() => Placeholder;
    public string OnPlayRequested() => Placeholder;
    public string OnUpdateRequested() => Placeholder;
    public string OnOpenFolderRequested() => Placeholder;
    public string OnDeleteModpackRequested() => Placeholder;
}
