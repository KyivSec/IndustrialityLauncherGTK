using Industriality.Backend.Models;

namespace Industriality.Backend.Abstractions;

public interface IModpackService
{
    Task<ModpackUpdateInfo> GetModpackUpdateInfoAsync(CancellationToken cancellationToken = default);
    Task DownloadAndInstallModpackAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default);
    bool IsModpackContentInstalled();
}
