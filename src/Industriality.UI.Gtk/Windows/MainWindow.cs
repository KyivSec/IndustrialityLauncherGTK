using Gtk;
using Industriality.UI.Gtk.Abstractions;
using Industriality.UI.Gtk.Styling;
using Industriality.UI.Gtk.Widgets;

namespace Industriality.UI.Gtk.Windows;

public sealed class MainWindow : Window
{
    private readonly IMainWindowActions _actions;
    private readonly Stack _contentStack;
    private readonly Button _modpackNavButton;
    private readonly Button _settingsNavButton;
    private static readonly string AssetsDirectoryPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets");

    public MainWindow(IMainWindowActions actions) : base("Industriality Launcher")
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));

        SetDefaultSize(1100, 700);
        WindowPosition = WindowPosition.Center;

        DeleteEvent += (_, args) =>
        {
            args.RetVal = false;
            Application.Quit();
        };

        FusionStyle.Apply();
        TrySetWindowIcon();

        var root = new Box(Orientation.Vertical, 0);
        Add(root);

        var body = new Box(Orientation.Horizontal, 0);
        root.PackStart(body, true, true, 0);

        var navigationPanel = BuildNavigationPanel(out _modpackNavButton, out _settingsNavButton);
        body.PackStart(navigationPanel, false, false, 0);

        _contentStack = new Stack
        {
            TransitionType = StackTransitionType.None
        };
        _contentStack.StyleContext.AddClass("fusion-content");
        _contentStack.AddNamed(BuildModpackPage(), "modpack");
        _contentStack.AddNamed(BuildSettingsPage(), "settings");

        body.PackStart(_contentStack, true, true, 0);

        Navigate("modpack");
    }

    private Widget BuildNavigationPanel(out Button modpackButton, out Button settingsButton)
    {
        var panel = new Box(Orientation.Vertical, 8)
        {
            WidthRequest = 200
        };
        panel.StyleContext.AddClass("fusion-nav");

        modpackButton = new Button("Modpack");
        settingsButton = new Button("Settings");

        modpackButton.Clicked += (_, _) => Navigate("modpack");
        settingsButton.Clicked += (_, _) => Navigate("settings");

        panel.PackStart(modpackButton, false, false, 0);
        panel.PackStart(settingsButton, false, false, 0);
        panel.PackStart(new Label(string.Empty), true, true, 0);

        return panel;
    }

    private Widget BuildModpackPage()
    {
        var root = new Box(Orientation.Vertical, 14);

        var bannerCard = new Frame { ShadowType = ShadowType.None };
        bannerCard.StyleContext.AddClass("fusion-card");
        bannerCard.HeightRequest = 430;

        var bannerLayout = new Box(Orientation.Vertical, 8)
        {
            MarginTop = 20,
            MarginBottom = 20,
            MarginStart = 20,
            MarginEnd = 20
        };
        var backgroundImageWidget = BuildBannerImage();

        var modpackName = new Label("Industriality")
        {
            Xalign = 0
        };
        modpackName.StyleContext.AddClass("fusion-section-title");

        var modpackVersion = new Label("Version 1.0.0")
        {
            Xalign = 0
        };

        var modpackInfo = new Label("GtkSharp fusion-style shell for modpack controls")
        {
            Xalign = 0
        };

        if (backgroundImageWidget is not null)
        {
            bannerLayout.PackStart(backgroundImageWidget, false, false, 0);
        }

        bannerLayout.PackEnd(modpackInfo, false, false, 0);
        bannerLayout.PackEnd(modpackVersion, false, false, 0);
        bannerLayout.PackEnd(modpackName, false, false, 0);

        bannerCard.Add(bannerLayout);
        root.PackStart(bannerCard, true, true, 0);

        var actionRow = new Box(Orientation.Horizontal, 10);
        actionRow.StyleContext.AddClass("fusion-action-row");

        var installButton = new Button("Install");
        var playButton = new Button("Play");
        var updateButton = new Button("Update");
        updateButton.StyleContext.AddClass("secondary");

        installButton.Clicked += (_, _) => ShowProgressAndNotice(_actions.OnInstallRequested());
        playButton.Clicked += (_, _) => ShowInfo(_actions.OnPlayRequested());
        updateButton.Clicked += (_, _) => ShowProgressAndNotice(_actions.OnUpdateRequested());

        actionRow.PackStart(installButton, true, true, 0);
        actionRow.PackStart(playButton, true, true, 0);
        actionRow.PackStart(updateButton, true, true, 0);

        root.PackStart(actionRow, false, false, 0);
        return root;
    }

    private Widget BuildSettingsPage()
    {
        var scroll = new ScrolledWindow
        {
            HscrollbarPolicy = PolicyType.Never,
            VscrollbarPolicy = PolicyType.Automatic
        };

        var root = new Box(Orientation.Vertical, 16)
        {
            MarginTop = 8,
            MarginBottom = 8
        };

        var profileCard = BuildProfileCard();
        var javaCard = BuildJavaCard();
        var actions = BuildSettingsActions();

        root.PackStart(profileCard, false, false, 0);
        root.PackStart(javaCard, false, false, 0);
        root.PackStart(actions, false, false, 0);
        root.PackStart(new Label(string.Empty), true, true, 0);

        scroll.Add(root);
        return scroll;
    }

    private Widget BuildProfileCard()
    {
        var card = new Frame { ShadowType = ShadowType.None };
        card.StyleContext.AddClass("fusion-card");

        var content = new Box(Orientation.Vertical, 10)
        {
            MarginTop = 14,
            MarginBottom = 14,
            MarginStart = 14,
            MarginEnd = 14
        };

        var title = new Label("Profile Settings")
        {
            Xalign = 0
        };
        title.StyleContext.AddClass("fusion-section-title");

        var grid = new Grid
        {
            ColumnSpacing = 10,
            RowSpacing = 8
        };

        var usernameLabel = new Label("Username")
        {
            Xalign = 0
        };
        var usernameEntry = new Entry
        {
            PlaceholderText = "Enter username"
        };
        usernameEntry.StyleContext.AddClass("fusion-input");

        grid.Attach(usernameLabel, 0, 0, 1, 1);
        grid.Attach(usernameEntry, 1, 0, 1, 1);

        content.PackStart(title, false, false, 0);
        content.PackStart(grid, false, false, 0);
        card.Add(content);

        return card;
    }

    private Widget BuildJavaCard()
    {
        var card = new Frame { ShadowType = ShadowType.None };
        card.StyleContext.AddClass("fusion-card");

        var content = new Box(Orientation.Vertical, 10)
        {
            MarginTop = 14,
            MarginBottom = 14,
            MarginStart = 14,
            MarginEnd = 14
        };

        var title = new Label("Java Settings")
        {
            Xalign = 0
        };
        title.StyleContext.AddClass("fusion-section-title");

        var grid = new Grid
        {
            ColumnSpacing = 10,
            RowSpacing = 10
        };

        var minLabel = new Label("Minimum RAM (MB)")
        {
            Xalign = 0
        };
        var minSpin = new SpinButton(512, 32768, 512)
        {
            Value = 512
        };
        minSpin.StyleContext.AddClass("fusion-input");

        var maxLabel = new Label("Maximum RAM (MB)")
        {
            Xalign = 0
        };
        var maxSpin = new SpinButton(1024, 65536, 512)
        {
            Value = 4096
        };
        maxSpin.StyleContext.AddClass("fusion-input");

        grid.Attach(minLabel, 0, 0, 1, 1);
        grid.Attach(minSpin, 1, 0, 1, 1);
        grid.Attach(maxLabel, 0, 1, 1, 1);
        grid.Attach(maxSpin, 1, 1, 1, 1);

        content.PackStart(title, false, false, 0);
        content.PackStart(grid, false, false, 0);
        card.Add(content);

        return card;
    }

    private Widget BuildSettingsActions()
    {
        var row = new Box(Orientation.Horizontal, 10);
        row.StyleContext.AddClass("fusion-settings-actions");

        var openFolder = new Button("Open Folder");
        var deleteModpack = new Button("Delete Modpack");

        openFolder.Clicked += (_, _) => ShowInfo(_actions.OnOpenFolderRequested());
        deleteModpack.Clicked += (_, _) => ShowInfo(_actions.OnDeleteModpackRequested());

        row.PackStart(openFolder, false, false, 0);
        row.PackStart(deleteModpack, false, false, 0);
        return row;
    }

    private void Navigate(string pageName)
    {
        _contentStack.VisibleChildName = pageName;

        _modpackNavButton.StyleContext.RemoveClass("selected");
        _settingsNavButton.StyleContext.RemoveClass("selected");

        if (string.Equals(pageName, "modpack", StringComparison.OrdinalIgnoreCase))
        {
            _modpackNavButton.StyleContext.AddClass("selected");
        }
        else
        {
            _settingsNavButton.StyleContext.AddClass("selected");
        }
    }

    private void ShowProgressAndNotice(string message)
    {
        using var progressWindow = new InstallProgressWindow();
        progressWindow.TransientFor = this;
        progressWindow.UpdateProgress("Placeholder", "Progress dialog shell only.", 35);
        progressWindow.ShowAll();

        while (Application.EventsPending())
        {
            Application.RunIteration();
        }

        ShowInfo(message);
        progressWindow.Destroy();
    }

    private void ShowInfo(string message)
    {
        using var dialog = new MessageDialog(
            this,
            DialogFlags.Modal,
            MessageType.Info,
            ButtonsType.Ok,
            message);
        dialog.Run();
        dialog.Destroy();
    }

    private void TrySetWindowIcon()
    {
        try
        {
            var iconPath = System.IO.Path.Combine(AssetsDirectoryPath, "icon.png");
            if (!File.Exists(iconPath))
            {
                return;
            }

            using var iconPixbuf = new Gdk.Pixbuf(iconPath);
            Icon = iconPixbuf.Copy();
        }
        catch
        {
        }
    }

    private Widget? BuildBannerImage()
    {
        try
        {
            var backgroundPath = System.IO.Path.Combine(AssetsDirectoryPath, "background.png");
            if (!File.Exists(backgroundPath))
            {
                return null;
            }

            var roundedImage = new RoundedImage(backgroundPath, cornerRadius: 18d, zoomFactor: 1.2d)
            {
                HeightRequest = 280,
                Hexpand = true,
                Halign = Align.Fill,
                Valign = Align.Start
            };

            return roundedImage;
        }
        catch
        {
            return null;
        }
    }
}
