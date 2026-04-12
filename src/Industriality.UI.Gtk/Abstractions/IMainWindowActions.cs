namespace Industriality.UI.Gtk.Abstractions;

public interface IMainWindowActions
{
    string OnInstallRequested();
    string OnPlayRequested();
    string OnUpdateRequested();
    string OnOpenFolderRequested();
    string OnDeleteModpackRequested();
}
