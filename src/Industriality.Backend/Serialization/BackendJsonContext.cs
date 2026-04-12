using System.Text.Json.Serialization;
using Industriality.Backend.Models;

namespace Industriality.Backend.Serialization;

[JsonSerializable(typeof(UiSettings))]
[JsonSerializable(typeof(GitHubReleaseInfo))]
internal sealed partial class BackendJsonContext : JsonSerializerContext
{
}
