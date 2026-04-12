using Gtk;
using Industriality.UI.Gtk.Actions;
using Industriality.UI.Gtk.Abstractions;
using Industriality.UI.Gtk.Windows;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddSingleton<IMainWindowActions, NoOpMainWindowActions>();
services.AddSingleton<MainWindow>();

using var serviceProvider = services.BuildServiceProvider();

Application.Init();

try
{
    var settings = Settings.Default;
    var animationProperty = settings?.GetType().GetProperty("GtkEnableAnimations");
    animationProperty?.SetValue(settings, false);
}
catch
{
}

var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
mainWindow.ShowAll();

Application.Run();
