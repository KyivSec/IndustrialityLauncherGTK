using System.Reflection;
using CmlLib.Core;
using Industriality.Backend.Models;

namespace Industriality.Backend.Services;

internal sealed class InstallService
{
    private LauncherSettings _settings;
    private readonly LauncherPaths _paths;

    public InstallService(LauncherSettings settings, LauncherPaths paths)
    {
        _settings = settings;
        _paths = paths;
    }

    public void UpdateSettings(LauncherSettings settings)
    {
        _settings = settings;
    }

    public async Task InstallVanillaAndNeoForgeAsync(
        string javaPath,
        IProgress<LauncherProgress>? progress,
        CancellationToken cancellationToken)
    {
        var minecraftPath = new MinecraftPath(_paths.GameDirectory);
        var launcher = new MinecraftLauncher(minecraftPath);

        LauncherShared.ReportProgress(progress, "Vanilla", "Installing vanilla Minecraft.", 60);
        await launcher.InstallAsync(_settings.MinecraftVersion).ConfigureAwait(false);

        LauncherShared.ReportProgress(progress, "NeoForge", "Installing NeoForge.", 82);
        await InstallNeoForgeAsync(
            minecraftPath,
            launcher,
            javaPath,
            _settings.MinecraftVersion,
            _settings.NeoForgeVersion,
            cancellationToken).ConfigureAwait(false);
    }

    public string VerifyInstalledVersion()
    {
        var versionId = VersionResolver.TryResolveInstalledVersionIdFromDisk(_settings, _paths);
        if (!string.IsNullOrWhiteSpace(versionId))
        {
            return versionId;
        }

        throw new DirectoryNotFoundException(
            "NeoForge installer finished, but expected version files were not found in: " +
            Path.Combine(_paths.GameDirectory, "versions"));
    }

    private static async Task InstallNeoForgeAsync(
        MinecraftPath minecraftPath,
        MinecraftLauncher launcher,
        string javaPath,
        string minecraftVersion,
        string neoForgeVersion,
        CancellationToken cancellationToken)
    {
        var installerType = Type.GetType(
            "CmlLib.Core.Installer.NeoForge.NeoForgeInstaller, CmlLib.Core.Installer.NeoForge",
            throwOnError: false)
            ?? throw new InvalidOperationException(
                "NeoForge installer type was not found. Ensure CmlLib.Core.Installer.NeoForge is referenced.");

        var optionsType = Type.GetType(
            "CmlLib.Core.Installer.NeoForge.NeoForgeInstallOptions, CmlLib.Core.Installer.NeoForge",
            throwOnError: false);

        object? options = null;
        if (optionsType is not null)
        {
            options = Activator.CreateInstance(optionsType);
            if (options is not null)
            {
                LauncherShared.SetPropertyIfExists(optionsType, options, "JavaPath", javaPath);
                LauncherShared.SetPropertyIfExists(optionsType, options, "JavaExecutablePath", javaPath);
                LauncherShared.SetPropertyIfExists(optionsType, options, "MinecraftPath", minecraftPath);
                LauncherShared.SetPropertyIfExists(optionsType, options, "Launcher", launcher);
                LauncherShared.SetPropertyIfExists(optionsType, options, "MinecraftLauncher", launcher);
            }
        }

        object? installerInstance = null;
        foreach (var constructor in installerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            var parameters = constructor.GetParameters();
            object?[]? arguments = parameters.Length switch
            {
                0 => [],
                1 when parameters[0].ParameterType.IsInstanceOfType(minecraftPath) => [minecraftPath],
                1 when parameters[0].ParameterType.IsInstanceOfType(launcher) => [launcher],
                1 when parameters[0].ParameterType == typeof(string) => [javaPath],
                2 when parameters[0].ParameterType.IsInstanceOfType(minecraftPath) &&
                       parameters[1].ParameterType == typeof(string) => [minecraftPath, javaPath],
                2 when parameters[0].ParameterType.IsInstanceOfType(launcher) &&
                       parameters[1].ParameterType == typeof(string) => [launcher, javaPath],
                _ => null
            };

            if (arguments is not null)
            {
                installerInstance = constructor.Invoke(arguments);
                break;
            }
        }

        var methods = installerType
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(method => method.Name is "Install" or "InstallAsync")
            .OrderByDescending(method => method.GetParameters().Length)
            .ToArray();

        foreach (var method in methods)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var arguments = BuildNeoForgeArguments(method.GetParameters(), minecraftVersion, neoForgeVersion, options);
            if (arguments is null)
            {
                continue;
            }

            var target = method.IsStatic ? null : installerInstance;
            if (!method.IsStatic && target is null)
            {
                continue;
            }

            var invocationResult = method.Invoke(target, arguments);
            if (invocationResult is Task task)
            {
                await task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            return;
        }

        throw new InvalidOperationException("Could not find a compatible NeoForge install method.");
    }

    private static object?[]? BuildNeoForgeArguments(
        ParameterInfo[] parameters,
        string minecraftVersion,
        string neoForgeVersion,
        object? options)
    {
        if (parameters.Length < 2 ||
            parameters[0].ParameterType != typeof(string) ||
            parameters[1].ParameterType != typeof(string))
        {
            return null;
        }

        var arguments = new object?[parameters.Length];
        var usedOptions = false;

        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];

            if (parameter.ParameterType == typeof(string))
            {
                arguments[index] = index == 0 ? minecraftVersion : index == 1 ? neoForgeVersion : null;
                if (arguments[index] is null)
                {
                    return null;
                }

                continue;
            }

            if (!usedOptions && options is not null && parameter.ParameterType.IsInstanceOfType(options))
            {
                arguments[index] = options;
                usedOptions = true;
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                arguments[index] = parameter.DefaultValue;
                continue;
            }

            return null;
        }

        return arguments;
    }
}
