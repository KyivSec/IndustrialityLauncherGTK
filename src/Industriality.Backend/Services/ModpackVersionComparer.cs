namespace Industriality.Backend.Services;

public static class ModpackVersionComparer
{
    public static bool IsUpdateAvailable(bool isInstalled, string currentVersion, string latestVersion)
    {
        return !isInstalled || !string.Equals(
            currentVersion?.Trim(),
            latestVersion?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}
