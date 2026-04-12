using Gtk;
using Industriality.Backend.Models;
using Industriality.UI.Gtk.Abstractions;
using Industriality.UI.Gtk.Models;
using Industriality.UI.Gtk.Styling;
using System.Diagnostics;
using System.Reflection;

namespace Industriality.UI.Gtk.Windows;

public sealed class MainWindow : Window
{
    private readonly IMainWindowActions _actions;
    private readonly Stack _contentStack;
    private readonly Button _modpackNavButton;
    private readonly Button _settingsNavButton;
    private readonly Button _installButton;
    private readonly Button _playButton;
    private readonly Button _updateButton;
    private readonly Button _openFolderButton;
    private readonly Button _deleteModpackButton;
    private readonly Label _modpackVersionLabel;
    private readonly Entry _usernameEntry;
    private readonly Entry _minRamEntry;
    private readonly Entry _maxRamEntry;
    private readonly List<Button> _memoryStepButtons = [];
    private static readonly Assembly AppAssembly = typeof(MainWindow).Assembly;
#pragma warning disable CS0612
    private readonly StatusIcon? _trayIcon;
#pragma warning restore CS0612
    private uint _runningPollSourceId;
    private bool _isHiddenToTrayByGame;

    private bool _isBusy;
    private bool _isInitializing;
    private bool _suppressSettingsCommit;
    private UiStatusSnapshot _status = new(false, "Not installed", string.Empty, false, false);

    public MainWindow(IMainWindowActions actions) : base("Industriality Launcher")
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));

        SetDefaultSize(586, 300);
        Resizable = false;
        WindowPosition = WindowPosition.Center;

        DeleteEvent += (_, args) =>
        {
            args.RetVal = true;
            Application.Quit();
        };

        FusionStyle.Apply();
        TrySetWindowIcon();
        _trayIcon = CreateTrayIcon();

        var root = new Box(Orientation.Vertical, 0);
        Add(root);

        var navigationPanel = BuildNavigationPanel(out _modpackNavButton, out _settingsNavButton);
        root.PackStart(navigationPanel, false, false, 0);

        _contentStack = new Stack
        {
            TransitionType = StackTransitionType.None
        };
        _contentStack.StyleContext.AddClass("fusion-content");

        _contentStack.AddNamed(
            BuildModpackPage(
                out _installButton,
                out _playButton,
                out _updateButton,
                out _modpackVersionLabel),
            "modpack");
        _contentStack.AddNamed(
            BuildSettingsPage(
                out _usernameEntry,
                out _minRamEntry,
                out _maxRamEntry,
                out _openFolderButton,
                out _deleteModpackButton),
            "settings");

        root.PackStart(_contentStack, true, true, 0);

        HookActionEvents();
        HookSettingsEvents();

        Shown += (_, _) => _ = RunSafeAsync(InitializeAsync);
        Destroyed += (_, _) => CleanupBackgroundSources();
        Navigate("modpack");
        ApplyStatusToUi();
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

    private Widget BuildModpackPage(
        out Button installButton,
        out Button playButton,
        out Button updateButton,
        out Label modpackVersionLabel)
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

        modpackVersionLabel = new Label("Version Not installed")
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
        modpackHeader.PackStart(modpackVersionLabel, false, false, 0);
        modpackHeader.PackStart(modpackInfo, false, false, 0);

        contentArea.PackEnd(modpackHeader, false, false, 0);
        root.PackStart(contentArea, true, true, 0);

        var actionRow = new Box(Orientation.Horizontal, 6);
        actionRow.StyleContext.AddClass("fusion-action-row");

        installButton = BuildActionButton("Install", "install.svg");
        playButton = BuildActionButton("Play", "play.svg");
        updateButton = BuildActionButton("Update", "update.svg");

        actionRow.PackStart(installButton, true, true, 0);
        actionRow.PackStart(playButton, true, true, 0);
        actionRow.PackStart(updateButton, true, true, 0);

        root.PackStart(actionRow, false, false, 0);
        return root;
    }

    private Widget BuildSettingsPage(
        out Entry usernameEntry,
        out Entry minRamEntry,
        out Entry maxRamEntry,
        out Button openFolderButton,
        out Button deleteModpackButton)
    {
        var root = new Box(Orientation.Vertical, 16)
        {
            MarginTop = 8,
            MarginBottom = 8,
            MarginStart = 8,
            MarginEnd = 8
        };

        var profileCard = BuildProfileCard(out usernameEntry);
        var javaCard = BuildJavaCard(out minRamEntry, out maxRamEntry);
        var actions = BuildSettingsActions(out openFolderButton, out deleteModpackButton);

        root.PackStart(profileCard, false, false, 0);
        root.PackStart(javaCard, false, false, 0);
        root.PackStart(actions, false, false, 0);

        return root;
    }

    private Widget BuildProfileCard(out Entry usernameEntry)
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

        usernameEntry = new Entry
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

    private Widget BuildJavaCard(out Entry minRamEntry, out Entry maxRamEntry)
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
        var minInput = BuildMemoryInput(out minRamEntry, 512, 512, 32768, 512);

        var maxLabel = new Label("Maximum RAM (MB)")
        {
            Xalign = 0
        };
        var maxInput = BuildMemoryInput(out maxRamEntry, 4096, 1024, 65536, 512);

        grid.Attach(minLabel, 0, 0, 1, 1);
        grid.Attach(minInput, 1, 0, 1, 1);
        grid.Attach(maxLabel, 0, 1, 1, 1);
        grid.Attach(maxInput, 1, 1, 1, 1);

        content.PackStart(title, false, false, 0);
        content.PackStart(grid, false, false, 0);
        card.Add(content);

        return card;
    }

    private Widget BuildSettingsActions(out Button openFolderButton, out Button deleteModpackButton)
    {
        var row = new Box(Orientation.Horizontal, 6);
        row.StyleContext.AddClass("fusion-settings-actions");

        openFolderButton = BuildActionButton("Open Folder", "folder.svg");
        deleteModpackButton = BuildActionButton("Delete Modpack", "delete.svg");

        row.PackStart(openFolderButton, false, false, 0);
        row.PackStart(deleteModpackButton, false, false, 0);
        return row;
    }

    private Widget BuildMemoryInput(out Entry entry, int initialValue, int minValue, int maxValue, int step)
    {
        var row = new Box(Orientation.Horizontal, 4);

        var entryInput = new Entry
        {
            WidthChars = 8,
            Text = initialValue.ToString()
        };
        entryInput.StyleContext.AddClass("fusion-input");
        entryInput.StyleContext.AddClass("fusion-memory-input");
        entry = entryInput;

        var plusButton = BuildSquareIconButton("plus.svg");
        var minusButton = BuildSquareIconButton("minus.svg");
        _memoryStepButtons.Add(plusButton);
        _memoryStepButtons.Add(minusButton);

        plusButton.Clicked += (_, _) =>
        {
            StepEntryValue(entryInput, step, minValue, maxValue);
            _ = RunSafeAsync(CommitSettingsFromUiAsync);
        };

        minusButton.Clicked += (_, _) =>
        {
            StepEntryValue(entryInput, -step, minValue, maxValue);
            _ = RunSafeAsync(CommitSettingsFromUiAsync);
        };

        row.PackStart(entryInput, false, false, 0);
        row.PackStart(plusButton, false, false, 0);
        row.PackStart(minusButton, false, false, 0);

        return row;
    }

    private static void StepEntryValue(Entry entry, int delta, int minValue, int maxValue)
    {
        var currentValue = ParseIntOrDefault(entry.Text, minValue);
        var nextValue = Math.Clamp(currentValue + delta, minValue, maxValue);
        entry.Text = nextValue.ToString();
    }

    private static int ParseIntOrDefault(string? value, int defaultValue)
    {
        return int.TryParse(value, out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    private void HookActionEvents()
    {
        _installButton.Clicked += (_, _) => _ = RunSafeAsync(() =>
            ExecuteProgressActionAsync((progress, token) => _actions.InstallAsync(progress, token), refreshStatus: true));
        _updateButton.Clicked += (_, _) => _ = RunSafeAsync(() =>
            ExecuteProgressActionAsync((progress, token) => _actions.UpdateAsync(progress, token), refreshStatus: true));
        _playButton.Clicked += (_, _) => _ = RunSafeAsync(HandlePlayStopClickedAsync);
        _openFolderButton.Clicked += (_, _) => _ = RunSafeAsync(() =>
            ExecuteActionAsync(token => _actions.OpenRootFolderAsync(token), refreshStatus: false));
        _deleteModpackButton.Clicked += (_, _) => _ = RunSafeAsync(() =>
            ExecuteActionAsync(token => _actions.DeleteModpackAsync(token), refreshStatus: true));
    }

    private void HookSettingsEvents()
    {
        _usernameEntry.FocusOutEvent += (_, _) => _ = RunSafeAsync(CommitSettingsFromUiAsync);
        _usernameEntry.Activated += (_, _) => _ = RunSafeAsync(CommitSettingsFromUiAsync);

        _minRamEntry.FocusOutEvent += (_, _) => _ = RunSafeAsync(CommitSettingsFromUiAsync);
        _minRamEntry.Activated += (_, _) => _ = RunSafeAsync(CommitSettingsFromUiAsync);

        _maxRamEntry.FocusOutEvent += (_, _) => _ = RunSafeAsync(CommitSettingsFromUiAsync);
        _maxRamEntry.Activated += (_, _) => _ = RunSafeAsync(CommitSettingsFromUiAsync);
    }

    private async Task InitializeAsync()
    {
        if (_isInitializing)
        {
            return;
        }

        _isInitializing = true;
        await SetBusyAsync(true);

        try
        {
            var settingsResult = await _actions.LoadSettingsAsync();
            if (settingsResult.Data is not null)
            {
                await InvokeOnUiAsync(() => ApplySettingsSnapshot(settingsResult.Data));
            }

            if (!settingsResult.Success)
            {
                await ShowDialogAsync(settingsResult.Message, settingsResult.Details, MessageType.Warning);
            }

            await RefreshStatusAsync(showFailures: true);
            StartRunningStatePolling();
        }
        finally
        {
            _isInitializing = false;
            await SetBusyAsync(false);
        }
    }

    private async Task RefreshStatusAsync(bool showFailures)
    {
        var previousRunning = _status.IsRunning;

        var statusResult = await _actions.GetStatusAsync();
        if (statusResult.Data is not null)
        {
            _status = statusResult.Data;
            await InvokeOnUiAsync(ApplyStatusToUi);

            if (previousRunning && !_status.IsRunning)
            {
                await InvokeOnUiAsync(RestoreFromTrayIfAutoHidden);
            }
        }

        if (!statusResult.Success && showFailures)
        {
            await ShowDialogAsync(statusResult.Message, statusResult.Details, MessageType.Warning);
        }
    }

    private async Task ExecuteActionAsync(
        Func<CancellationToken, Task<UiActionResult>> action,
        bool refreshStatus,
        bool showSuccessMessage = true)
    {
        if (_isBusy)
        {
            return;
        }

        await SetBusyAsync(true);
        try
        {
            var result = await action(CancellationToken.None);
            if (!result.Success || showSuccessMessage)
            {
                await ShowResultDialogAsync(result);
            }

            if (refreshStatus)
            {
                await RefreshStatusAsync(showFailures: true);
            }
        }
        finally
        {
            await SetBusyAsync(false);
        }
    }

    private async Task HandlePlayStopClickedAsync()
    {
        var wasRunning = _status.IsRunning;

        await ExecuteActionAsync(
            token => wasRunning ? _actions.StopAsync(token) : _actions.PlayAsync(token),
            refreshStatus: true,
            showSuccessMessage: false);

        if (!wasRunning && _status.IsRunning)
        {
            await InvokeOnUiAsync(MinimizeToTrayForGame);
        }
    }

    private async Task ExecuteProgressActionAsync(
        Func<IProgress<LauncherProgress>?, CancellationToken, Task<UiActionResult>> action,
        bool refreshStatus)
    {
        if (_isBusy)
        {
            return;
        }

        InstallProgressWindow? progressWindow = null;
        var progressWindowClosed = false;

        var progressReporter = new Progress<LauncherProgress>(progress =>
        {
            Application.Invoke((_, _) =>
            {
                progressWindow?.UpdateProgress(progress.Stage, progress.Message, progress.Percent);
            });
        });

        await SetBusyAsync(true);
        try
        {
            await InvokeOnUiAsync(() =>
            {
                progressWindow = new InstallProgressWindow
                {
                    TransientFor = this
                };
                progressWindow.ShowAll();
            });

            var result = await action(progressReporter, CancellationToken.None);

            await InvokeOnUiAsync(() =>
            {
                progressWindow?.Destroy();
                progressWindowClosed = true;
            });

            await ShowResultDialogAsync(result);

            if (refreshStatus)
            {
                await RefreshStatusAsync(showFailures: true);
            }
        }
        finally
        {
            if (!progressWindowClosed)
            {
                await InvokeOnUiAsync(() =>
                {
                    progressWindow?.Destroy();
                });
            }

            await SetBusyAsync(false);
        }
    }

    private async Task CommitSettingsFromUiAsync()
    {
        if (_suppressSettingsCommit || _isInitializing)
        {
            return;
        }

        var snapshot = ReadSettingsFromUi();
        await InvokeOnUiAsync(() => ApplySettingsSnapshot(snapshot));

        var saveResult = await _actions.SaveSettingsAsync(snapshot);
        if (!saveResult.Success)
        {
            await ShowDialogAsync(saveResult.Message, saveResult.Details, MessageType.Warning);
        }
    }

    private UiSettingsSnapshot ReadSettingsFromUi()
    {
        var username = string.IsNullOrWhiteSpace(_usernameEntry.Text)
            ? "Player"
            : _usernameEntry.Text.Trim();

        var minRam = Math.Max(512, ParseIntOrDefault(_minRamEntry.Text, 512));
        var maxRamDefault = Math.Max(minRam, 4096);
        var maxRam = Math.Max(minRam, ParseIntOrDefault(_maxRamEntry.Text, maxRamDefault));

        return new UiSettingsSnapshot(username, minRam, maxRam);
    }

    private void ApplySettingsSnapshot(UiSettingsSnapshot snapshot)
    {
        _suppressSettingsCommit = true;
        try
        {
            _usernameEntry.Text = snapshot.Username;
            _minRamEntry.Text = snapshot.MinRamMb.ToString();
            _maxRamEntry.Text = snapshot.MaxRamMb.ToString();
        }
        finally
        {
            _suppressSettingsCommit = false;
        }
    }

    private async Task SetBusyAsync(bool busy)
    {
        _isBusy = busy;
        await InvokeOnUiAsync(ApplyStatusToUi);
    }

    private void ApplyStatusToUi()
    {
        _modpackVersionLabel.Text = _status.IsInstalled
            ? $"Version {_status.InstalledVersion}"
            : "Version Not installed";

        SetButtonContent(_playButton, _status.IsRunning ? "Stop" : "Play", _status.IsRunning ? "stop.svg" : "play.svg");

        _installButton.Sensitive = !_isBusy && !_status.IsInstalled && !_status.IsRunning;
        _playButton.Sensitive = !_isBusy && _status.IsInstalled;
        _updateButton.Sensitive = !_isBusy && _status.IsInstalled && _status.UpdateAvailable && !_status.IsRunning;
        _openFolderButton.Sensitive = !_isBusy;
        _deleteModpackButton.Sensitive = !_isBusy;

        _usernameEntry.Sensitive = !_isBusy;
        _minRamEntry.Sensitive = !_isBusy;
        _maxRamEntry.Sensitive = !_isBusy;

        foreach (var button in _memoryStepButtons)
        {
            button.Sensitive = !_isBusy;
        }
    }

    private void StartRunningStatePolling()
    {
        if (_runningPollSourceId != 0)
        {
            return;
        }

        _runningPollSourceId = GLib.Timeout.Add(1200, () =>
        {
            _ = RunSafeAsync(PollRunningStateAsync);
            return true;
        });
    }

    private async Task PollRunningStateAsync()
    {
        if (_isBusy || _isInitializing)
        {
            return;
        }

        var isRunning = await _actions.IsGameRunningAsync();
        if (isRunning == _status.IsRunning)
        {
            return;
        }

        var wasRunning = _status.IsRunning;
        _status = _status with { IsRunning = isRunning };
        await InvokeOnUiAsync(ApplyStatusToUi);

        if (wasRunning && !isRunning)
        {
            await InvokeOnUiAsync(RestoreFromTrayIfAutoHidden);
        }
    }

#pragma warning disable CS0612
    private StatusIcon? CreateTrayIcon()
    {
        try
        {
            var iconPixbuf = LoadAssetPixbuf("icon.png");
            if (iconPixbuf is null)
            {
                return null;
            }

            var icon = new StatusIcon(iconPixbuf)
            {
                Visible = false,
                TooltipText = "Industriality Launcher"
            };
            icon.Activate += (_, _) =>
            {
                ShowAll();
                Present();
                _isHiddenToTrayByGame = false;
                if (!_status.IsRunning)
                {
                    icon.Visible = false;
                }
            };

            return icon;
        }
        catch
        {
            return null;
        }
    }
#pragma warning restore CS0612

    private void MinimizeToTrayForGame()
    {
#pragma warning disable CS0612
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.Visible = true;
        _isHiddenToTrayByGame = true;
        Hide();
        NotifyMinimizedToTray();
#pragma warning restore CS0612
    }

    private void RestoreFromTrayIfAutoHidden()
    {
#pragma warning disable CS0612
        if (!_isHiddenToTrayByGame)
        {
            if (_trayIcon is not null && !_status.IsRunning)
            {
                _trayIcon.Visible = false;
            }
            return;
        }

        ShowAll();
        Present();
        _isHiddenToTrayByGame = false;

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = _status.IsRunning;
        }
#pragma warning restore CS0612
    }

    private void CleanupBackgroundSources()
    {
        if (_runningPollSourceId != 0)
        {
            GLib.Source.Remove(_runningPollSourceId);
            _runningPollSourceId = 0;
        }

        if (_trayIcon is not null)
        {
#pragma warning disable CS0612
            _trayIcon.Visible = false;
#pragma warning restore CS0612
        }
    }

    private void NotifyMinimizedToTray()
    {
        const string title = "Industriality Launcher";
        const string message = "Launcher minimized to tray while modpack is running.";

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var command =
                    "$ErrorActionPreference='Stop';" +
                    "$title='Industriality Launcher';" +
                    "$msg='Launcher minimized to tray while modpack is running.';" +
                    "[Windows.UI.Notifications.ToastNotificationManager,Windows.UI.Notifications,ContentType=WindowsRuntime] > $null;" +
                    "$template=[Windows.UI.Notifications.ToastTemplateType]::ToastText02;" +
                    "$xml=[Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent($template);" +
                    "$texts=$xml.GetElementsByTagName('text');" +
                    "$null=$texts.Item(0).AppendChild($xml.CreateTextNode($title));" +
                    "$null=$texts.Item(1).AppendChild($xml.CreateTextNode($msg));" +
                    "$toast=[Windows.UI.Notifications.ToastNotification]::new($xml);" +
                    "[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Industriality Launcher').Show($toast);";

                _ = Task.Run(() => TryStartProcess("powershell", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\""));
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                var escapedTitle = title.Replace("\"", "\\\"");
                var escapedMessage = message.Replace("\"", "\\\"");
                _ = Task.Run(() => TryStartProcess("osascript", $"-e \"display notification \\\"{escapedMessage}\\\" with title \\\"{escapedTitle}\\\"\""));
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                var escapedTitle = title.Replace("\"", "\\\"");
                var escapedMessage = message.Replace("\"", "\\\"");
                _ = Task.Run(() => TryStartProcess("notify-send", $"\"{escapedTitle}\" \"{escapedMessage}\""));
            }
        }
        catch
        {
        }
    }

    private static void TryStartProcess(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch
        {
        }
    }

    private async Task ShowResultDialogAsync(UiActionResult result)
    {
        await ShowDialogAsync(
            result.Message,
            result.Details,
            result.Success ? MessageType.Info : MessageType.Warning);
    }

    private Task ShowDialogAsync(string message, string? details, MessageType type)
    {
        return InvokeOnUiAsync(() =>
        {
            var text = string.IsNullOrWhiteSpace(details)
                ? message
                : $"{message}\n\nDetails:\n{details}";

            var dialog = new MessageDialog(
                this,
                DialogFlags.Modal,
                type,
                ButtonsType.Ok,
                text);
            dialog.StyleContext.AddClass("fusion-popup");
            if (dialog.MessageArea is not null)
            {
                dialog.MessageArea.StyleContext.AddClass("fusion-popup-body");
            }

            try
            {
                dialog.Run();
            }
            finally
            {
                dialog.Destroy();
            }
        });
    }

    private Task RunSafeAsync(Func<Task> action)
    {
        return Task.Run(async () =>
        {
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                await ShowDialogAsync("Unexpected launcher error.", exception.ToString(), MessageType.Error);
            }
        });
    }

    private Task InvokeOnUiAsync(System.Action action)
    {
        var completion = new TaskCompletionSource<object?>();
        Application.Invoke((_, _) =>
        {
            try
            {
                action();
                completion.TrySetResult(null);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });

        return completion.Task;
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

    private void TrySetWindowIcon()
    {
        try
        {
            var iconPixbuf = LoadAssetPixbuf("icon.png");
            if (iconPixbuf is null)
            {
                return;
            }

            Icon = iconPixbuf.Copy();
        }
        catch
        {
        }
    }

    private Button BuildActionButton(string text, string iconFileName)
    {
        var button = new Button();
        SetButtonContent(button, text, iconFileName);
        return button;
    }

    private void SetButtonContent(Button button, string text, string iconFileName)
    {
        if (button.Child is Widget existingChild)
        {
            button.Remove(existingChild);
        }

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
        content.ShowAll();
        button.ShowAll();
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
            var source = LoadAssetPixbuf(iconFileName);
            if (source is null)
            {
                return null;
            }

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

    private static Gdk.Pixbuf? LoadAssetPixbuf(string fileName)
    {
        try
        {
            var resourceName = FindAssetResourceName(fileName);
            if (resourceName is null)
            {
                return null;
            }

            using var stream = AppAssembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return null;
            }

            return new Gdk.Pixbuf(stream);
        }
        catch
        {
            return null;
        }
    }

    private static string? FindAssetResourceName(string fileName)
    {
        var suffix = ".Assets." + fileName.Replace('\\', '.').Replace('/', '.');
        return AppAssembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
