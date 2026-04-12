using Industriality.Backend.Models;

namespace Industriality.Backend.Services;

public static class VersionResolver
{
    public static string? TryResolveInstalledVersionIdFromDisk(LauncherSettings settings, LauncherPaths paths)
    {
        var versionsDirectory = Path.Combine(paths.GameDirectory, "versions");
        if (!Directory.Exists(versionsDirectory))
        {
            return null;
        }

        var candidates = new[]
        {
            $"neoforge-{settings.NeoForgeVersion}",
            $"{settings.MinecraftVersion}-neoforge-{settings.NeoForgeVersion}",
            settings.NeoForgeVersion
        };

        foreach (var versionId in candidates)
        {
            var versionDirectory = Path.Combine(versionsDirectory, versionId);
            var versionJsonPath = Path.Combine(versionDirectory, versionId + ".json");

            if (Directory.Exists(versionDirectory) && File.Exists(versionJsonPath))
            {
                return versionId;
            }
        }

        return null;
    }
}
