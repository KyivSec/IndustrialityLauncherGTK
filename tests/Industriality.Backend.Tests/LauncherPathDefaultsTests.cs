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
    public void GetDefaultRootDirectory_ForMac_UsesApplicationBaseDirectory()
    {
        var result = LauncherPathDefaults.GetDefaultRootDirectory(
            OSPlatform.OSX,
            userProfile: "/Users/admin",
            applicationData: "/unused",
            xdgDataHome: null);

        Assert.Equal(NormalizePathSeparators(Path.GetFullPath(AppContext.BaseDirectory)), NormalizePathSeparators(result));
    }

    [Fact]
    public void GetDefaultRootDirectory_ForLinux_UsesUsrBin()
    {
        var result = LauncherPathDefaults.GetDefaultRootDirectory(
            OSPlatform.Linux,
            userProfile: "/home/admin",
            applicationData: "/unused",
            xdgDataHome: "/home/admin/.xdg-data");

        Assert.Equal("/usr/bin/IndustrialityLauncher", NormalizePathSeparators(result));
    }

    private static string NormalizePathSeparators(string value)
    {
        return value.Replace('\\', '/');
    }
}
