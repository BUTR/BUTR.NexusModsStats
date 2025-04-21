using BUTR.NexusModsStats.Models;

using System.Text.Json.Serialization;

namespace BUTR.NexusModsStats.Utils;

[JsonSerializable(typeof(ShieldsResponseBody))]
public partial class ShieldsJsonSerializerContext : JsonSerializerContext;