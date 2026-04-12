namespace Industriality.Backend.Services;

public static class JavaExecutableLocator
{
    public static string? FindJavaExecutable(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return null;
        }

        if (OperatingSystem.IsWindows())
        {
            var javaw = Directory.EnumerateFiles(rootDirectory, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(javaw))
            {
                return javaw;
            }

            return Directory.EnumerateFiles(rootDirectory, "java.exe", SearchOption.AllDirectories).FirstOrDefault();
        }

        return Directory
            .EnumerateFiles(rootDirectory, "java", SearchOption.AllDirectories)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), "java", StringComparison.Ordinal));
    }
}
