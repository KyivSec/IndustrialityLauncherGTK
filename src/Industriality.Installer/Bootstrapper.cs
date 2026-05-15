using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Versioning;

namespace Industriality.Installer;

internal static class Bootstrapper
{
    private const string PayloadResource = "Industriality.Installer.payload.zip";
    private const string VersionResource = "Industriality.Installer.payload.version";

    public static void Run()
    {
        var embeddedVersion = ReadEmbeddedVersion();
        var installRoot = PathResolver.InstallRoot;
        var installedVersion = ReadInstalledVersion();

        if (!string.Equals(embeddedVersion, installedVersion, StringComparison.Ordinal)
            || !File.Exists(PathResolver.LauncherExecutable))
        {
            ExtractPayload(installRoot, embeddedVersion);
        }

        LaunchAndDetach();
    }

    private static string ReadEmbeddedVersion()
    {
        using var stream = OpenResource(VersionResource);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }

    private static string? ReadInstalledVersion()
    {
        var path = PathResolver.InstalledVersionFile;
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static void ExtractPayload(string installRoot, string version)
    {
        var parent = Path.GetDirectoryName(installRoot)!;
        Directory.CreateDirectory(parent);

        var staging = Path.Combine(parent, $".{Path.GetFileName(installRoot)}.tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);

        try
        {
            using (var stream = OpenResource(PayloadResource))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ExtractArchive(archive, staging);
            }

            File.WriteAllText(Path.Combine(staging, ".payload-version"), version);

            if (Directory.Exists(installRoot))
            {
                var backup = installRoot + ".old-" + Guid.NewGuid().ToString("N");
                Directory.Move(installRoot, backup);
                try { Directory.Delete(backup, recursive: true); } catch { /* best effort */ }
            }

            Directory.Move(staging, installRoot);
        }
        catch
        {
            try { Directory.Delete(staging, recursive: true); } catch { /* best effort */ }
            throw;
        }
    }

    private static void ExtractArchive(ZipArchive archive, string destination)
    {
        var fullDest = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(fullDest, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Zip entry escapes destination: {entry.FullName}");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);

            if (!OperatingSystem.IsWindows())
            {
                ApplyUnixMode(entry, target);
            }
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static void ApplyUnixMode(ZipArchiveEntry entry, string target)
    {
        var mode = (int)(entry.ExternalAttributes >> 16) & 0xFFF;
        if (mode != 0)
        {
            File.SetUnixFileMode(target, (UnixFileMode)mode);
            return;
        }

        if (LooksExecutable(target))
        {
            File.SetUnixFileMode(target,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static bool LooksExecutable(string path)
    {
        var name = Path.GetFileName(path);
        if (name.Equals("IndustrialityLauncher", StringComparison.Ordinal)) return true;
        if (name.Contains(".so", StringComparison.Ordinal)) return true;
        if (name.EndsWith(".dylib", StringComparison.Ordinal)) return true;
        var dir = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
        return dir.Equals("bin", StringComparison.Ordinal);
    }

    private static void LaunchAndDetach()
    {
        var psi = new ProcessStartInfo
        {
            FileName = PathResolver.LauncherExecutable,
            WorkingDirectory = PathResolver.InstallRoot,
            UseShellExecute = false,
        };
        Process.Start(psi);
    }

    private static Stream OpenResource(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(
                $"Missing embedded resource '{name}'. Did the installer build run scripts/build-installer.ps1?");
        return stream;
    }
}
