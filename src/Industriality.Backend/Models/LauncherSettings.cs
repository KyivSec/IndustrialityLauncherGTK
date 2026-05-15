using System.Globalization;
using Industriality.Backend.Services;

namespace Industriality.Backend.Models;

public sealed class LauncherSettings
{
    public string MinecraftVersion { get; set; } = "1.21.1";
    public string NeoForgeVersion { get; set; } = "21.1.220";
    public int JavaFeatureVersion { get; set; } = 25;
    public string PlayerName { get; set; } = "Player";
    public int MinRamMb { get; set; } = 512;
    public int MaxRamMb { get; set; } = 4096;
    public string RootDirectory { get; set; } = string.Empty;

    public static LauncherSettings CreateDefault()
    {
        var settings = new LauncherSettings();
        settings.Normalize();
        return settings;
    }

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(RootDirectory))
        {
            RootDirectory = LauncherPathDefaults.GetDefaultRootDirectoryForCurrentPlatform();
        }

        RootDirectory = Path.GetFullPath(RootDirectory.Trim());
        PlayerName = string.IsNullOrWhiteSpace(PlayerName) ? "Player" : PlayerName.Trim();
        JavaFeatureVersion = Math.Max(8, JavaFeatureVersion);
        MinRamMb = Math.Max(512, MinRamMb);
        MaxRamMb = Math.Max(MinRamMb, MaxRamMb);
        MinecraftVersion = string.IsNullOrWhiteSpace(MinecraftVersion) ? "1.21.1" : MinecraftVersion.Trim();
        NeoForgeVersion = string.IsNullOrWhiteSpace(NeoForgeVersion) ? "21.1.219" : NeoForgeVersion.Trim();
    }

    public string GetJavaRuntimeKey()
    {
        return JavaFeatureVersion.ToString(CultureInfo.InvariantCulture);
    }

    public LauncherSettings Clone()
    {
        return new LauncherSettings
        {
            MinecraftVersion = MinecraftVersion,
            NeoForgeVersion = NeoForgeVersion,
            JavaFeatureVersion = JavaFeatureVersion,
            PlayerName = PlayerName,
            MinRamMb = MinRamMb,
            MaxRamMb = MaxRamMb,
            RootDirectory = RootDirectory
        };
    }
}
