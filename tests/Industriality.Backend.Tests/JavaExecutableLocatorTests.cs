using Industriality.Backend.Services;

namespace Industriality.Backend.Tests;

public sealed class JavaExecutableLocatorTests : IDisposable
{
    private readonly string _tempDirectory;

    public JavaExecutableLocatorTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "IndustrialityBackendTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void FindJavaExecutable_OnWindowsPrefersJavawAndFindsJava()
    {
        var binDirectory = Path.Combine(_tempDirectory, "jdk", "bin");
        Directory.CreateDirectory(binDirectory);

        var javaPath = Path.Combine(binDirectory, "java.exe");
        var javawPath = Path.Combine(binDirectory, "javaw.exe");
        File.WriteAllText(javaPath, string.Empty);
        File.WriteAllText(javawPath, string.Empty);

        var result = JavaExecutableLocator.FindJavaExecutable(_tempDirectory);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(javawPath, result);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Fact]
    public void FindJavaExecutable_OnUnixFindsJava()
    {
        var binDirectory = Path.Combine(_tempDirectory, "jdk", "bin");
        Directory.CreateDirectory(binDirectory);

        var unixJavaPath = Path.Combine(binDirectory, "java");
        File.WriteAllText(unixJavaPath, string.Empty);

        var result = JavaExecutableLocator.FindJavaExecutable(_tempDirectory);

        if (OperatingSystem.IsWindows())
        {
            Assert.Null(result);
        }
        else
        {
            Assert.Equal(unixJavaPath, result);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
