using System.Runtime.InteropServices;

namespace Industriality.Backend.Services;

public static class LauncherPathDefaults
{
    public static string GetDefaultRootDirectoryForCurrentPlatform()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

        if (OperatingSystem.IsWindows())
        {
            return GetDefaultRootDirectory(OSPlatform.Windows, userProfile, appData, xdgDataHome);
        }

        if (OperatingSystem.IsMacOS())
        {
            return GetDefaultRootDirectory(OSPlatform.OSX, userProfile, appData, xdgDataHome);
        }

        return GetDefaultRootDirectory(OSPlatform.Linux, userProfile, appData, xdgDataHome);
    }

    public static string GetDefaultRootDirectory(
        OSPlatform osPlatform,
        string userProfile,
        string applicationData,
        string? xdgDataHome)
    {
        if (osPlatform == OSPlatform.Windows)
        {
            return Path.Combine(applicationData, "IndustrialityLauncher");
        }

        if (osPlatform == OSPlatform.OSX)
        {
            return Path.Combine(userProfile, "Library", "Application Support", "IndustrialityLauncher");
        }

        if (!string.IsNullOrWhiteSpace(xdgDataHome))
        {
            return Path.Combine(xdgDataHome, "IndustrialityLauncher");
        }

        return Path.Combine(userProfile, ".local", "share", "IndustrialityLauncher");
    }
}
