using Industriality.Backend.Models;

namespace Industriality.Backend.Services;

public static class VersionResolver
{
    public static string? TryResolveInstalledVersionIdFromDisk(LauncherSettings settings, LauncherPaths paths)
    {
        var versionsDirectory = paths.VersionsDirectory;
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

        var fallback = Directory
            .EnumerateFiles(versionsDirectory, "*.json", SearchOption.AllDirectories)
            .Select(filePath =>
            {
                var id = Path.GetFileNameWithoutExtension(filePath);
                var score = 0;

                if (id.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                {
                    score += 100;
                }

                if (id.Contains(settings.NeoForgeVersion, StringComparison.OrdinalIgnoreCase))
                {
                    score += 10;
                }

                return new
                {
                    Id = id,
                    Score = score,
                    LastWrite = File.GetLastWriteTimeUtc(filePath)
                };
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.LastWrite)
            .Select(item => item.Id)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        return null;
    }
}
