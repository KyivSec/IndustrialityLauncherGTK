using System.Text.Json;
using Industriality.Backend.Abstractions;
using Industriality.Backend.Models;
using Industriality.Backend.Serialization;

namespace Industriality.Backend.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = BackendJsonContext.Default
    };

    private static readonly BackendJsonContext SerializerContext = new(SerializerOptions);

    public async Task<UiSettings> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new UiSettings();
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize(json, BackendJsonContext.Default.UiSettings);
            return result ?? new UiSettings();
        }
        catch
        {
            return new UiSettings();
        }
    }

    public async Task SaveAsync(string filePath, UiSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must be provided.", nameof(filePath));
        }

        ArgumentNullException.ThrowIfNull(settings);

        var minRam = Math.Max(512, settings.MinRamMb);
        var maxRam = Math.Max(minRam, settings.MaxRamMb);

        var normalized = new UiSettings
        {
            Username = settings.Username?.Trim() ?? string.Empty,
            MinRamMb = minRam,
            MaxRamMb = maxRam
        };

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(normalized, SerializerContext.UiSettings);
        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
    }
}
