using System.Runtime.InteropServices;

namespace Industriality.Installer;

internal static class PathResolver
{
    private const string AppFolderName = "IndustrialityLauncher";
    private const string AppFolderNameUnix = "industriality";

    public static string InstallRoot { get; } = ResolveInstallRoot();

    public static string LauncherExecutable
    {
        get
        {
            var name = OperatingSystem.IsWindows() ? "IndustrialityLauncher.exe" : "IndustrialityLauncher";
            return Path.Combine(InstallRoot, name);
        }
    }

    public static string InstalledVersionFile => Path.Combine(InstallRoot, ".payload-version");

    private static string ResolveInstallRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, AppFolderName);
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", AppFolderName);
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(xdg))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            xdg = Path.Combine(home, ".local", "share");
        }
        return Path.Combine(xdg, AppFolderNameUnix);
    }
}
