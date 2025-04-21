using System.Text.Json.Serialization;

namespace BUTR.NexusModsStats.Models;

public sealed record NexusModsModInfoResponse([property: JsonPropertyName("version")] string Version);