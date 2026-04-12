using Industriality.Backend.Models;

namespace Industriality.Backend.Abstractions;

public interface IJavaRuntimeResolver
{
    int JavaFeatureVersion { get; }

    Task<string> ResolveJavaExecutablePathAsync(
        IProgress<LauncherProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
