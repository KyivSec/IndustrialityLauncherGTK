using Industriality.Backend.Services;

namespace Industriality.Backend.Tests;

public sealed class ModpackVersionComparerTests
{
    [Fact]
    public void IsUpdateAvailable_ReturnsTrueWhenNotInstalled()
    {
        var result = ModpackVersionComparer.IsUpdateAvailable(false, "1.0.0", "1.0.0");
        Assert.True(result);
    }

    [Fact]
    public void IsUpdateAvailable_ReturnsFalseWhenInstalledAndVersionMatches()
    {
        var result = ModpackVersionComparer.IsUpdateAvailable(true, "v1.2.0", "v1.2.0");
        Assert.False(result);
    }

    [Fact]
    public void IsUpdateAvailable_ReturnsTrueWhenInstalledAndVersionDiffers()
    {
        var result = ModpackVersionComparer.IsUpdateAvailable(true, "v1.2.0", "v1.3.0");
        Assert.True(result);
    }
}
