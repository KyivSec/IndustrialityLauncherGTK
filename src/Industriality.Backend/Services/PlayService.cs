using System.Diagnostics;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using Industriality.Backend.Abstractions;
using Industriality.Backend.Models;

namespace Industriality.Backend.Services;

internal sealed class PlayService
{
    private LauncherSettings _settings;
    private readonly LauncherPaths _paths;
    private readonly IJavaRuntimeResolver _javaRuntimeResolver;
    private readonly object _processSync = new();
    private Process? _runningProcess;

    public PlayService(LauncherSettings settings, LauncherPaths paths, IJavaRuntimeResolver javaRuntimeResolver)
    {
        _settings = settings;
        _paths = paths;
        _javaRuntimeResolver = javaRuntimeResolver;
    }

    public void UpdateSettings(LauncherSettings settings)
    {
        _settings = settings;
    }

    public async Task PlayAsync(string versionId, CancellationToken cancellationToken)
    {
        if (IsGameRunning())
        {
            return;
        }

        var javaPath = await _javaRuntimeResolver.ResolveJavaExecutablePathAsync(
            progress: null,
            cancellationToken).ConfigureAwait(false);

        var minecraftPath = new MinecraftPath(_paths.GameDirectory);
        var launcher = new MinecraftLauncher(minecraftPath);

        var launchOption = new MLaunchOption
        {
            Session = MSession.CreateOfflineSession(_settings.PlayerName)
        };

        LauncherShared.SetPropertyIfExists(typeof(MLaunchOption), launchOption, "JavaPath", javaPath);
        LauncherShared.SetPropertyIfExists(typeof(MLaunchOption), launchOption, "MinimumRamMb", _settings.MinRamMb);
        LauncherShared.SetPropertyIfExists(typeof(MLaunchOption), launchOption, "MaximumRamMb", _settings.MaxRamMb);
        LauncherShared.ApplyJvmMemoryArgumentsIfPossible(launchOption, _settings.MinRamMb, _settings.MaxRamMb);

        var processObject = await launcher.BuildProcessAsync(versionId, launchOption).ConfigureAwait(false)
            ?? throw new InvalidOperationException("CmlLib returned null from BuildProcessAsync.");

        var startInfo = ExtractStartInfo(processObject, javaPath);
        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        process.Exited += (_, _) =>
        {
            lock (_processSync)
            {
                if (ReferenceEquals(_runningProcess, process))
                {
                    _runningProcess = null;
                }
            }

            process.Dispose();
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Minecraft process failed to start.");
        }

        lock (_processSync)
        {
            _runningProcess = process;
        }
    }

    public bool IsGameRunning()
    {
        lock (_processSync)
        {
            return _runningProcess is { HasExited: false };
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Process? process;

        lock (_processSync)
        {
            process = _runningProcess;
            _runningProcess = null;
        }

        if (process is null || process.HasExited)
        {
            return Task.CompletedTask;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }

        return Task.CompletedTask;
    }

    private ProcessStartInfo ExtractStartInfo(object processObject, string javaPath)
    {
        var startInfoProperty = processObject.GetType().GetProperty("StartInfo");
        if (startInfoProperty?.GetValue(processObject) is not ProcessStartInfo startInfo)
        {
            throw new InvalidOperationException("Could not extract ProcessStartInfo from built Minecraft process.");
        }

        startInfo.FileName = javaPath;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = false;
        startInfo.RedirectStandardError = false;

        var javaBinDirectory = Path.GetDirectoryName(javaPath)
            ?? throw new InvalidOperationException("Could not resolve Java bin directory.");
        var javaHomeDirectory = Directory.GetParent(javaBinDirectory)?.FullName
            ?? throw new InvalidOperationException("Could not resolve Java home directory.");

        startInfo.WorkingDirectory = string.IsNullOrWhiteSpace(startInfo.WorkingDirectory)
            ? _paths.GameDirectory
            : startInfo.WorkingDirectory;

        startInfo.Environment["JAVA_HOME"] = javaHomeDirectory;

        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var existingPath = startInfo.Environment.TryGetValue("PATH", out var pathValue)
            ? pathValue ?? string.Empty
            : string.Empty;

        if (!existingPath.Split(separator, StringSplitOptions.RemoveEmptyEntries)
            .Any(value => string.Equals(value.Trim(), javaBinDirectory, StringComparison.OrdinalIgnoreCase)))
        {
            startInfo.Environment["PATH"] = string.IsNullOrWhiteSpace(existingPath)
                ? javaBinDirectory
                : javaBinDirectory + separator + existingPath;
        }

        return startInfo;
    }
}
