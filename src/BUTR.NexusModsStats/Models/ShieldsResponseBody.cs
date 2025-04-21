using BUTR.NexusModsStats.Utils;

namespace BUTR.NexusModsStats.Models;

public sealed record ShieldsResponseBody(int SchemaVersion, string Label, string Message, string Color)
{
    public static IResult Success(string label, string message) =>
        Results.Json(new ShieldsResponseBody(1, label, message, "yellow"), ShieldsJsonSerializerContext.Default.ShieldsResponseBody);

    public static IResult Error(string label, string message) =>
        Results.Json(new ShieldsResponseBody(1, label, message, "red"), ShieldsJsonSerializerContext.Default.ShieldsResponseBody);
}