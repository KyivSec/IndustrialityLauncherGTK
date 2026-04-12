namespace Industriality.Backend.Models;

public sealed class LauncherRuntimeMetadata
{
    public string VersionId { get; set; } = string.Empty;
    public string MainClass { get; set; } = string.Empty;
    public string[] ClasspathEntries { get; set; } = [];
    public string AssetsDirectory { get; set; } = string.Empty;
    public string AssetIndexId { get; set; } = string.Empty;
    public string NativesDirectory { get; set; } = string.Empty;
    public string[] ExtraJvmArguments { get; set; } = [];
    public string[] ExtraGameArguments { get; set; } = [];
}
