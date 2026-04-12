using System.Runtime.InteropServices;

namespace Industriality.Backend.Services;

public static class AdoptiumUrlBuilder
{
    public static string BuildBinaryUrl(int featureVersion, string os, string arch)
    {
        if (featureVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(featureVersion), "Feature version must be positive.");
        }

        if (string.IsNullOrWhiteSpace(os))
        {
            throw new ArgumentException("OS must be provided.", nameof(os));
        }

        if (string.IsNullOrWhiteSpace(arch))
        {
            throw new ArgumentException("Architecture must be provided.", nameof(arch));
        }

        var normalizedOs = os.Trim().ToLowerInvariant();
        var normalizedArch = arch.Trim().ToLowerInvariant();

        return $"https://api.adoptium.net/v3/binary/latest/{featureVersion}/ga/{normalizedOs}/{normalizedArch}/jdk/hotspot/normal/eclipse";
    }

    public static string BuildForCurrentPlatform(int featureVersion)
    {
        return BuildBinaryUrl(featureVersion, GetCurrentOsIdentifier(), GetCurrentArchIdentifier());
    }

    public static string GetCurrentOsIdentifier()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "mac";
        }

        return "linux";
    }

    public static string GetCurrentArchIdentifier()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "aarch64",
            Architecture.X86 => "x32",
            _ => "x64"
        };
    }
}
