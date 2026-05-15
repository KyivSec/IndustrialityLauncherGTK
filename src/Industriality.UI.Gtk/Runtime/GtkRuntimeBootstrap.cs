using System.Runtime.InteropServices;

namespace Industriality.UI.Gtk.Runtime;

internal static class GtkRuntimeBootstrap
{
    public static void ConfigureEnvironment()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var runtimeRoot = ResolveRuntimeRoot(baseDirectory);
        if (string.IsNullOrWhiteSpace(runtimeRoot))
        {
            return;
        }

        var binDirectory = ResolveBinaryDirectory(runtimeRoot);
        var libDirectory = Path.Combine(runtimeRoot, "lib");
        var shareDirectory = Path.Combine(runtimeRoot, "share");

        if (OperatingSystem.IsWindows())
        {
            PrependEnvironmentPath("PATH", binDirectory);
            SetIfDirectoryExists("GTK_EXE_PREFIX", runtimeRoot);
            SetIfDirectoryExists("GTK_DATA_PREFIX", runtimeRoot);
            SetIfDirectoryExists("GTK_PATH", runtimeRoot);
            SetIfDirectoryExists("XDG_DATA_DIRS", shareDirectory);
            SetIfDirectoryExists("GSETTINGS_SCHEMA_DIR", Path.Combine(shareDirectory, "glib-2.0", "schemas"));
            ConfigurePixbufVariables(libDirectory);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            PrependEnvironmentPath("LD_LIBRARY_PATH", libDirectory);
            PrependEnvironmentPath("PATH", binDirectory);
            SetIfDirectoryExists("XDG_DATA_DIRS", shareDirectory);
            SetIfDirectoryExists("GSETTINGS_SCHEMA_DIR", Path.Combine(shareDirectory, "glib-2.0", "schemas"));
            ConfigurePixbufVariables(libDirectory);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            PrependEnvironmentPath("DYLD_LIBRARY_PATH", libDirectory);
            PrependEnvironmentPath("PATH", binDirectory);
            SetIfDirectoryExists("XDG_DATA_DIRS", shareDirectory);
            SetIfDirectoryExists("GSETTINGS_SCHEMA_DIR", Path.Combine(shareDirectory, "glib-2.0", "schemas"));
            ConfigurePixbufVariables(libDirectory);
        }
    }

    private static string? ResolveRuntimeRoot(string baseDirectory)
    {
        var embeddedRoot = Path.Combine(baseDirectory, "gtk-runtime");
        if (HasGtkRuntime(embeddedRoot))
        {
            return embeddedRoot;
        }

        if (HasGtkRuntime(baseDirectory))
        {
            return baseDirectory;
        }

        return null;
    }

    private static bool HasGtkRuntime(string rootDirectory)
    {
        var binDirectory = ResolveBinaryDirectory(rootDirectory);

        if (OperatingSystem.IsWindows())
        {
            return File.Exists(Path.Combine(binDirectory, "libgtk-3-0.dll"));
        }

        if (OperatingSystem.IsLinux())
        {
            return Directory.EnumerateFiles(binDirectory, "libgtk-3*.so*", SearchOption.TopDirectoryOnly).Any();
        }

        if (OperatingSystem.IsMacOS())
        {
            return Directory.EnumerateFiles(binDirectory, "libgtk-3*.dylib", SearchOption.TopDirectoryOnly).Any();
        }

        return false;
    }

    private static string ResolveBinaryDirectory(string rootDirectory)
    {
        var embeddedBinDirectory = Path.Combine(rootDirectory, "bin");
        if (Directory.Exists(embeddedBinDirectory))
        {
            return embeddedBinDirectory;
        }

        return rootDirectory;
    }

    private static void ConfigurePixbufVariables(string libDirectory)
    {
        var loaderDirectory = Path.Combine(libDirectory, "gdk-pixbuf-2.0", "2.10.0", "loaders");
        var loaderCache = Path.Combine(libDirectory, "gdk-pixbuf-2.0", "2.10.0", "loaders.cache");

        SetIfDirectoryExists("GDK_PIXBUF_MODULEDIR", loaderDirectory);
        if (File.Exists(loaderCache))
        {
            Environment.SetEnvironmentVariable("GDK_PIXBUF_MODULE_FILE", loaderCache);
        }
    }

    private static void SetIfDirectoryExists(string variableName, string path)
    {
        if (Directory.Exists(path))
        {
            Environment.SetEnvironmentVariable(variableName, path);
        }
    }

    private static void PrependEnvironmentPath(string variableName, string pathToAdd)
    {
        if (!Directory.Exists(pathToAdd))
        {
            return;
        }

        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        var existing = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(existing))
        {
            Environment.SetEnvironmentVariable(variableName, pathToAdd);
            return;
        }

        var alreadyExists = existing
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => string.Equals(part, pathToAdd, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            return;
        }

        Environment.SetEnvironmentVariable(variableName, pathToAdd + separator + existing);
    }
}
