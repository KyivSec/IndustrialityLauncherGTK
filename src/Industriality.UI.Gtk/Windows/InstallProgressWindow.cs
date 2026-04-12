using Gtk;

namespace Industriality.UI.Gtk.Windows;

public sealed class InstallProgressWindow : Window
{
    private readonly Label _stageLabel;
    private readonly Label _messageLabel;
    private readonly ProgressBar _progressBar;

    public InstallProgressWindow() : base("Installing")
    {
        SetDefaultSize(480, 220);
        Resizable = false;
        WindowPosition = WindowPosition.CenterOnParent;

        var root = new Box(Orientation.Vertical, 12)
        {
            MarginTop = 20,
            MarginBottom = 20,
            MarginStart = 20,
            MarginEnd = 20
        };

        _stageLabel = new Label("Preparing...")
        {
            Xalign = 0
        };

        _messageLabel = new Label("Starting installation.")
        {
            Xalign = 0,
            Wrap = true
        };

        _progressBar = new ProgressBar
        {
            Fraction = 0d
        };

        root.PackStart(_stageLabel, false, false, 0);
        root.PackStart(_messageLabel, false, false, 0);
        root.PackStart(_progressBar, false, false, 0);

        Add(root);
    }

    public void UpdateProgress(string stage, string message, double percent)
    {
        _stageLabel.Text = stage;
        _messageLabel.Text = message;

        if (percent < 0)
        {
            _progressBar.Pulse();
            return;
        }

        _progressBar.Fraction = Math.Clamp(percent / 100d, 0d, 1d);
    }
}
