using BUTR.NexusModsStats.Models;
using BUTR.NexusModsStats.Services;
using BUTR.NexusModsStats.Utils;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BUTR.NexusModsStats.Extensions;

public static class DownloadsExtensions
{
    public static WebApplicationBuilder AddDownloadsEndpoint(this WebApplicationBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IEndpointDefinition, DownloadsEndpointDefinition>());
        builder.Services.AddHttpClient<INexusModsStatisticsClient, NexusModsStatisticsClient>().ConfigureHttpClient((_, client) =>
        {
            client.BaseAddress = new Uri("https://staticstats.nexusmods.com/");
            client.DefaultRequestHeaders.Add("User-Agent", HttpUtils.UserAgent);
        }).AddCustomResilienceHandler();
        return builder;
    }

    public class DownloadsEndpointDefinition : IEndpointDefinition
    {
        public void RegisterEndpoints(WebApplication app)
        {
            app.MapGet("/downloads", static async (
                [FromQuery] string type, [FromQuery] string gameId, [FromQuery] string modId,
                [FromServices] INexusModsStatisticsClient client,
                CancellationToken ct) =>
            {
                if (!RequestValidation.IsValidId(gameId) || !RequestValidation.IsValidId(modId))
                    return ShieldsResponseBody.Error("", "Invalid 'gameId' or 'modId'!");

                var label = type switch
                {
                    "unique" => "Unique Downloads",
                    "total" => "Total Downloads",
                    "views" => "Total Views",
                    _ => "",
                };

                if (string.IsNullOrEmpty(label))
                    return ShieldsResponseBody.Error("", "Unknown type!");

                var download = await client.GetLiveDownloadCountsAsync(gameId, modId, ct);
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