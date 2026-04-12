using Industriality.Backend.Models;

namespace Industriality.Backend.Abstractions;

public interface ISettingsStore
{
    Task<UiSettings> LoadAsync(string filePath, CancellationToken cancellationToken = default);
    Task SaveAsync(string filePath, UiSettings settings, CancellationToken cancellationToken = default);
}
