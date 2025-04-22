using BUTR.NexusModsStats.Models;

using System.Text.Json.Serialization;

namespace BUTR.NexusModsStats.Utils;

[JsonSerializable(typeof(ShieldsResponseBody))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class ShieldsJsonSerializerContext : JsonSerializerContext;