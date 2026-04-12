namespace Industriality.Backend.Models;

public sealed record ModpackUpdateInfo(
    bool IsInstalled,
    string CurrentVersion,
    string LatestVersion,
    bool UpdateAvailable);
