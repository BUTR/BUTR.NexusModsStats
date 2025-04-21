using BUTR.NexusModsStats.Models;
using BUTR.NexusModsStats.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

using System.Diagnostics.CodeAnalysis;

namespace BUTR.NexusModsStats.Extensions;

public static class DownloadsExtensions
{
    public static WebApplicationBuilder AddDownloadsEndpoint(this WebApplicationBuilder builder)
    {
        var assemblyName = typeof(DownloadsExtensions).Assembly.GetName();
        var userAgent = $"{assemblyName.Name ?? "ERROR"} v{assemblyName.Version?.ToString() ?? "ERROR"} (github.com/BUTR)";

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IEndpointDefinition, DownloadsEndpointDefinition>());
        builder.Services.AddHttpClient<INexusModsStatisticsClient, NexusModsStatisticsClient>().ConfigureHttpClient((_, client) =>
        {
            client.BaseAddress = new Uri("https://staticstats.nexusmods.com/");
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        }).AddCustomResilienceHandler();
        return builder;
    }

    public class DownloadsEndpointDefinition : IEndpointDefinition
    {
        [RequiresUnreferencedCode("Minimal API")]
        [RequiresDynamicCode("Minimal API")]
        public void RegisterEndpoints(WebApplication app)
        {
            app.MapGet("/downloads", static async (
                [FromQuery] string type, [FromQuery] string gameId, [FromQuery] string modId,
                [FromServices] INexusModsStatisticsClient client,
                CancellationToken ct) =>
            {
                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(modId))
                    return ShieldsResponseBody.Error("", "Missing required query parameters!");

                var label = type switch
                {
                    "unique" => "Unique Downloads",
                    "total" => "Total Downloads",
                    "views" => "Total Views",
                    _ => "",
                };

                if (string.IsNullOrEmpty(label))
                    return ShieldsResponseBody.Error("", "Unknown type!");

                var download = await client.GetLiveDownloadCountsAsync(gameId, ct).FirstOrDefaultAsync(x => x.Id == modId, ct);
                if (download is null)
                    return ShieldsResponseBody.Error(label, "mod not found!");

                var message = type switch
                {
                    "unique" => download.UniqueDownloads,
                    "total" => download.TotalDownloads,
                    "views" => download.TotalViews,
                    _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
                };
                return ShieldsResponseBody.Success(label, message.ToString());
            }).CacheOutput();
        }
    }
}