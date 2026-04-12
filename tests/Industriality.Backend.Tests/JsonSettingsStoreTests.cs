using System.Text.Json;
using Industriality.Backend.Models;
using Industriality.Backend.Services;

namespace Industriality.Backend.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _settingsPath;

    public JsonSettingsStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "IndustrialityBackendTests", Guid.NewGuid().ToString("N"));
        _settingsPath = Path.Combine(_tempDirectory, "launcher-settings.json");
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsAndNormalizesRamBounds()
    {
        var store = new JsonSettingsStore();
        var settings = new UiSettings
        {
            Username = "PlayerOne",
            MinRamMb = 256,
            MaxRamMb = 512
        };

        await store.SaveAsync(_settingsPath, settings);
        var loaded = await store.LoadAsync(_settingsPath);

        Assert.Equal("PlayerOne", loaded.Username);
        Assert.Equal(512, loaded.MinRamMb);
        Assert.Equal(512, loaded.MaxRamMb);

        var json = await File.ReadAllTextAsync(_settingsPath);
        var parsed = JsonSerializer.Deserialize<UiSettings>(json);
        Assert.NotNull(parsed);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
