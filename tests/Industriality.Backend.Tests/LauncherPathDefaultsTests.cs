using System.Runtime.InteropServices;
using Industriality.Backend.Services;

namespace Industriality.Backend.Tests;

public sealed class LauncherPathDefaultsTests
{
    [Fact]
    public void GetDefaultRootDirectory_ForWindows_UsesAppData()
    {
        var result = LauncherPathDefaults.GetDefaultRootDirectory(
            OSPlatform.Windows,
            userProfile: @"C:\Users\Admin",
            applicationData: @"C:\Users\Admin\AppData\Roaming",
            xdgDataHome: null);

        Assert.Equal(
            @"C:\Users\Admin\AppData\Roaming\IndustrialityLauncher",
            result);
    }

    [Fact]
    public void GetDefaultRootDirectory_ForMac_UsesLibraryApplicationSupport()
    {
        var result = LauncherPathDefaults.GetDefaultRootDirectory(
            OSPlatform.OSX,
            userProfile: "/Users/admin",
            applicationData: "/unused",
            xdgDataHome: null);

        Assert.Equal(
            "/Users/admin/Library/Application Support/IndustrialityLauncher",
            NormalizePathSeparators(result));
    }

    [Fact]
    public void GetDefaultRootDirectory_ForLinuxPrefersXdgDataHome()
    {
        var result = LauncherPathDefaults.GetDefaultRootDirectory(
            OSPlatform.Linux,
            userProfile: "/home/admin",
            applicationData: "/unused",
            xdgDataHome: "/home/admin/.xdg-data");

        Assert.Equal("/home/admin/.xdg-data/IndustrialityLauncher", NormalizePathSeparators(result));
    }

    [Fact]
    public void GetDefaultRootDirectory_ForLinuxFallsBackToLocalShare()
    {
        var result = LauncherPathDefaults.GetDefaultRootDirectory(
            OSPlatform.Linux,
            userProfile: "/home/admin",
            applicationData: "/unused",
            xdgDataHome: null);

        Assert.Equal("/home/admin/.local/share/IndustrialityLauncher", NormalizePathSeparators(result));
    }

    private static string NormalizePathSeparators(string value)
    {
        return value.Replace('\\', '/');
    }
}
