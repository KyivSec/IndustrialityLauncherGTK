using System.Diagnostics;
using System.Reflection;
using Industriality.Backend.Models;

namespace Industriality.Backend.Services;

internal static class LauncherShared
{
    public static void ReportProgress(
        IProgress<LauncherProgress>? progress,
        string stage,
        string message,
        double percent)
    {
        progress?.Report(new LauncherProgress(stage, message, Math.Clamp(percent, 0d, 100d)));
    }

    public static HttpClient CreateGitHubHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IndustrialityLauncher/2.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public static void SetPropertyIfExists(Type targetType, object target, string propertyName, object? value)
    {
        if (value is null)
        {
            return;
        }

        var property = targetType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        if (property.PropertyType.IsAssignableFrom(value.GetType()))
        {
            property.SetValue(target, value);
        }
    }

    public static void ApplyJvmMemoryArgumentsIfPossible(object launchOption, int minRamMb, int maxRamMb)
    {
        var launchOptionType = launchOption.GetType();
        var arguments = new[]
        {
            $"-Xms{minRamMb}m",
            $"-Xmx{maxRamMb}m"
        };

        var property = launchOptionType.GetProperty("JvmArguments", BindingFlags.Public | BindingFlags.Instance)
            ?? launchOptionType.GetProperty("GameJvmArguments", BindingFlags.Public | BindingFlags.Instance)
            ?? launchOptionType.GetProperty("AdditionalJvmArguments", BindingFlags.Public | BindingFlags.Instance);

        if (property is null || !property.CanWrite)
        {
            return;
        }

        if (property.PropertyType == typeof(string[]))
        {
            var existing = property.GetValue(launchOption) as string[] ?? Array.Empty<string>();
            property.SetValue(launchOption, existing.Concat(arguments).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            return;
        }

        if (typeof(IList<string>).IsAssignableFrom(property.PropertyType) &&
            property.GetValue(launchOption) is IList<string> list)
        {
            foreach (var argument in arguments)
            {
                if (!list.Contains(argument, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(argument);
                }
            }
        }
    }

    public static void OpenFolderCrossPlatform(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{directoryPath}\"",
                UseShellExecute = true
            });
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"\"{directoryPath}\"",
                UseShellExecute = false
            });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "xdg-open",
            Arguments = $"\"{directoryPath}\"",
            UseShellExecute = false
        });
    }
}
