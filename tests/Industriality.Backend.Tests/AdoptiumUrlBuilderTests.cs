using Industriality.Backend.Services;

namespace Industriality.Backend.Tests;

public sealed class AdoptiumUrlBuilderTests
{
    [Theory]
    [InlineData(25, "windows", "x64", "https://api.adoptium.net/v3/binary/latest/25/ga/windows/x64/jdk/hotspot/normal/eclipse")]
    [InlineData(25, "linux", "aarch64", "https://api.adoptium.net/v3/binary/latest/25/ga/linux/aarch64/jdk/hotspot/normal/eclipse")]
    [InlineData(25, "mac", "x64", "https://api.adoptium.net/v3/binary/latest/25/ga/mac/x64/jdk/hotspot/normal/eclipse")]
    public void BuildBinaryUrl_BuildsExpectedEndpoint(int featureVersion, string os, string arch, string expected)
    {
        var result = AdoptiumUrlBuilder.BuildBinaryUrl(featureVersion, os, arch);
        Assert.Equal(expected, result);
    }
}
