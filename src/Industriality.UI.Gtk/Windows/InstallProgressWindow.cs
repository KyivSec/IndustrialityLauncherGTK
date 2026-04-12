using Gtk;

namespace Industriality.UI.Gtk.Windows;

public sealed class InstallProgressWindow : Window
{
    private readonly Label _stageLabel;
    private readonly Label _messageLabel;
    private readonly ProgressBar _progressBar;

    public InstallProgressWindow() : base("Installing")
    {
        SetDefaultSize(420, 160);
        Resizable = false;
        WindowPosition = WindowPosition.CenterOnParent;

        var root = new Box(Orientation.Vertical, 8)
        {
            MarginTop = 14,
            MarginBottom = 14,
            MarginStart = 14,
            MarginEnd = 14
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
            Fraction = 0d,
            WidthRequest = 360,
            HeightRequest = 30,
            ShowText = true,
            Text = "0%"
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
            _progressBar.Text = "Working...";
            return;
        }

        var clampedPercent = Math.Clamp(percent, 0d, 100d);
        _progressBar.Fraction = clampedPercent / 100d;
        _progressBar.Text = $"{Math.Round(clampedPercent):0}%";
    }
}
