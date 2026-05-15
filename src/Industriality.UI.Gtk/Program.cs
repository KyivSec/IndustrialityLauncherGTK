using Gtk;
using Industriality.Backend;
using Industriality.Backend.Abstractions;
using Industriality.Backend.Services;
using Industriality.UI.Gtk.Actions;
using Industriality.UI.Gtk.Abstractions;
using Industriality.UI.Gtk.Runtime;
using Industriality.UI.Gtk.Windows;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddSingleton<ILauncherBackend, LauncherBackend>();
services.AddSingleton<ISettingsStore, JsonSettingsStore>();
services.AddSingleton<IMainWindowActions, BackendMainWindowActions>();
services.AddSingleton<MainWindow>();

using var serviceProvider = services.BuildServiceProvider();

GtkRuntimeBootstrap.ConfigureEnvironment();
Application.Init();

var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
mainWindow.ShowAll();

Application.Run();
