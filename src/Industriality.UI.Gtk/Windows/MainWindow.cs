using Gtk;
using Industriality.UI.Gtk.Abstractions;
using Industriality.UI.Gtk.Styling;

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

        SetDefaultSize(586, 350);
        Resizable = false;
        WindowPosition = WindowPosition.Center;

        DeleteEvent += (_, args) =>
        {
            args.RetVal = true;
            Application.Quit();
        };

        FusionStyle.Apply();
        TrySetWindowIcon();

        var root = new Box(Orientation.Vertical, 0);
        Add(root);

        var navigationPanel = BuildNavigationPanel(out _modpackNavButton, out _settingsNavButton);
        root.PackStart(navigationPanel, false, false, 0);

        _contentStack = new Stack
        {
            TransitionType = StackTransitionType.None
        };
        _contentStack.StyleContext.AddClass("fusion-content");
        _contentStack.AddNamed(BuildModpackPage(), "modpack");
        _contentStack.AddNamed(BuildSettingsPage(), "settings");

        root.PackStart(_contentStack, true, true, 0);

        Navigate("modpack");
    }

    private Widget BuildNavigationPanel(out Button modpackButton, out Button settingsButton)
    {
        var navBar = new Box(Orientation.Horizontal, 0);
        navBar.StyleContext.AddClass("fusion-nav");

        var panel = new Box(Orientation.Horizontal, 8);

        modpackButton = BuildActionButton("Modpack", "play.svg");
        settingsButton = BuildActionButton("Settings", "settings.svg");
        modpackButton.WidthRequest = 120;
        settingsButton.WidthRequest = 120;

        modpackButton.Clicked += (_, _) => Navigate("modpack");
        settingsButton.Clicked += (_, _) => Navigate("settings");

        panel.PackStart(modpackButton, false, false, 0);
        panel.PackStart(settingsButton, false, false, 0);
        navBar.PackStart(new Label(string.Empty), true, true, 0);
        navBar.PackStart(panel, false, false, 0);
        navBar.PackStart(new Label(string.Empty), true, true, 0);

        return navBar;
    }

    private Widget BuildModpackPage()
    {
        var root = new Box(Orientation.Vertical, 8)
        {
            MarginTop = 8,
            MarginBottom = 8,
            MarginStart = 8,
            MarginEnd = 8
        };
        var contentArea = new Box(Orientation.Vertical, 0)
        {
            Hexpand = true,
            Vexpand = true
        };

        var modpackHeader = new Box(Orientation.Vertical, 6)
        {
            Halign = Align.Start,
            Valign = Align.End,
            MarginBottom = 10
        };

        var modpackName = new Label("Industriality")
        {
            Xalign = 0
        };
        modpackName.StyleContext.AddClass("fusion-section-title");

        var modpackVersion = new Label("Version 1.0.0")
        {
            Xalign = 0
        };

        var modpackInfo = new Label("POLITICAL MODDED SMP | FACTORIES | GUNS | FACTIONS | AIRCRAFT")
        {
            Xalign = 0
        };

        var launcherIcon = BuildSizedIcon("icon.png", 72);
        if (launcherIcon is not null)
        {
            launcherIcon.Halign = Align.Start;
            modpackHeader.PackStart(launcherIcon, false, false, 0);
        }

        modpackHeader.PackStart(modpackName, false, false, 0);
        modpackHeader.PackStart(modpackVersion, false, false, 0);
        modpackHeader.PackStart(modpackInfo, false, false, 0);
        contentArea.PackEnd(modpackHeader, false, false, 0);
        root.PackStart(contentArea, true, true, 0);

        var actionRow = new Box(Orientation.Horizontal, 6);
        actionRow.StyleContext.AddClass("fusion-action-row");

        var installButton = BuildActionButton("Install", "install.svg");
        var playButton = BuildActionButton("Play", "play.svg");
        var updateButton = BuildActionButton("Update", "update.svg");

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
        var root = new Box(Orientation.Vertical, 16)
        {
            MarginTop = 8,
            MarginBottom = 8,
            MarginStart = 8,
            MarginEnd = 8
        };

        var profileCard = BuildProfileCard();
        var javaCard = BuildJavaCard();
        var actions = BuildSettingsActions();

        root.PackStart(profileCard, false, false, 0);
        root.PackStart(javaCard, false, false, 0);
        root.PackStart(actions, false, false, 0);

        return root;
    }

    private Widget BuildProfileCard()
    {
        var card = new Frame { ShadowType = ShadowType.None };
        card.StyleContext.AddClass("fusion-card");

        var content = new Box(Orientation.Vertical, 10)
        {
            MarginTop = 8,
            MarginBottom = 8,
            MarginStart = 8,
            MarginEnd = 8
        };

        var title = BuildSectionTitle("Profile Settings", "account.svg");

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
            MarginTop = 8,
            MarginBottom = 8,
            MarginStart = 8,
            MarginEnd = 8
        };

        var title = BuildSectionTitle("Java Settings", "memory.svg");

        var grid = new Grid
        {
            ColumnSpacing = 6,
            RowSpacing = 6
        };

        var minLabel = new Label("Minimum RAM (MB)")
        {
            Xalign = 0
        };
        var minInput = BuildMemoryInput(initialValue: 512, minValue: 512, maxValue: 32768, step: 512);

        var maxLabel = new Label("Maximum RAM (MB)")
        {
            Xalign = 0
        };
        var maxInput = BuildMemoryInput(initialValue: 4096, minValue: 1024, maxValue: 65536, step: 512);

        grid.Attach(minLabel, 0, 0, 1, 1);
        grid.Attach(minInput, 1, 0, 1, 1);
        grid.Attach(maxLabel, 0, 1, 1, 1);
        grid.Attach(maxInput, 1, 1, 1, 1);

        content.PackStart(title, false, false, 0);
        content.PackStart(grid, false, false, 0);
        card.Add(content);

        return card;
    }

    private Widget BuildSettingsActions()
    {
        var row = new Box(Orientation.Horizontal, 6);
        row.StyleContext.AddClass("fusion-settings-actions");

        var openFolder = BuildActionButton("Open Folder", "folder.svg");
        var deleteModpack = BuildActionButton("Delete Modpack", "delete.svg");

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
        var progressWindow = new InstallProgressWindow();
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
        var dialog = new MessageDialog(
            this,
            DialogFlags.Modal,
            MessageType.Info,
            ButtonsType.Ok,
            message);
        try
        {
            dialog.Run();
        }
        finally
        {
            dialog.Destroy();
        }
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

    private Button BuildActionButton(string text, string iconFileName)
    {
        var button = new Button();

        var content = new Box(Orientation.Horizontal, 8)
        {
            Halign = Align.Center,
            Valign = Align.Center
        };

        var icon = BuildInlineIcon(iconFileName);
        if (icon is not null)
        {
            content.PackStart(icon, false, false, 0);
        }

        content.PackStart(new Label(text), false, false, 0);
        button.Add(content);

        return button;
    }

    private Widget BuildMemoryInput(int initialValue, int minValue, int maxValue, int step)
    {
        var row = new Box(Orientation.Horizontal, 4);

        var input = new Entry
        {
            WidthChars = 8,
            Text = initialValue.ToString()
        };
        input.StyleContext.AddClass("fusion-input");
        input.StyleContext.AddClass("fusion-memory-input");

        var minusButton = BuildSquareIconButton("minus.svg");
        var plusButton = BuildSquareIconButton("plus.svg");

        var value = initialValue;

        void SetValue(int candidate)
        {
            value = Math.Clamp(candidate, minValue, maxValue);
            input.Text = value.ToString();
        }

        void CommitTypedValue()
        {
            if (int.TryParse(input.Text, out var parsed))
            {
                SetValue(parsed);
                return;
            }

            SetValue(value);
        }

        minusButton.Clicked += (_, _) => SetValue(value - step);
        plusButton.Clicked += (_, _) => SetValue(value + step);
        input.FocusOutEvent += (_, _) =>
        {
            CommitTypedValue();
        };
        input.Activated += (_, _) => CommitTypedValue();

        row.PackStart(input, false, false, 0);
        row.PackStart(plusButton, false, false, 0);
        row.PackStart(minusButton, false, false, 0);

        return row;
    }

    private Button BuildSquareIconButton(string iconFileName)
    {
        var button = new Button
        {
            WidthRequest = 26,
            HeightRequest = 26
        };
        button.StyleContext.AddClass("fusion-step-button");

        var icon = BuildInlineIcon(iconFileName);
        if (icon is not null)
        {
            button.Add(icon);
        }
        else
        {
            button.Add(new Label("?"));
        }

        return button;
    }

    private Widget BuildSectionTitle(string text, string iconFileName)
    {
        var titleRow = new Box(Orientation.Horizontal, 6)
        {
            Halign = Align.Start
        };

        var icon = BuildInlineIcon(iconFileName);
        if (icon is not null)
        {
            titleRow.PackStart(icon, false, false, 0);
        }

        var label = new Label(text)
        {
            Xalign = 0
        };
        label.StyleContext.AddClass("fusion-section-title");
        titleRow.PackStart(label, false, false, 0);

        return titleRow;
    }

    private Image? BuildInlineIcon(string iconFileName)
    {
        return BuildSizedIcon(iconFileName, 16);
    }

    private Image? BuildSizedIcon(string iconFileName, int size)
    {
        try
        {
            var iconPath = System.IO.Path.Combine(AssetsDirectoryPath, iconFileName);
            if (!File.Exists(iconPath))
            {
                return null;
            }

            var source = new Gdk.Pixbuf(iconPath);
            var iconPixbuf = source.Width == size && source.Height == size
                ? source
                : source.ScaleSimple(size, size, Gdk.InterpType.Bilinear) ?? source;

            if (!ReferenceEquals(source, iconPixbuf))
            {
                source.Dispose();
            }

            return new Image(iconPixbuf);
        }
        catch
        {
            return null;
        }
    }
}
