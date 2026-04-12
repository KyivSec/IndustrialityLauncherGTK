using System.Diagnostics;
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
