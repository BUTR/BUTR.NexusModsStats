using BUTR.NexusModsStats.Models;
using BUTR.NexusModsStats.Services;
using BUTR.NexusModsStats.Utils;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BUTR.NexusModsStats.Extensions;

public static class ModVersionExtensions
{
    public static WebApplicationBuilder AddModVersionEndpoint(this WebApplicationBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IEndpointDefinition, ModVersionShieldsEndpointDefinition>());
        builder.Services.AddHttpClient<INexusModsApiClient, NexusModsApiClient>().ConfigureHttpClient((_, client) =>
        {
            client.BaseAddress = new Uri("https://api.nexusmods.com/");
            client.DefaultRequestHeaders.Add("User-Agent", HttpUtils.UserAgent);
        }).AddHttpMessageHandler<NexusModsAuthorizationHandler>().AddNexusModsResilienceHandler();
        builder.Services.AddTransient<NexusModsAuthorizationHandler>();

        return builder;
    }

    public class ModVersionShieldsEndpointDefinition : IEndpointDefinition
    {
        public void RegisterEndpoints(WebApplication app)
        {
            app.MapGet("/mod-version", static async (
                [FromQuery] string gameId,
                [FromQuery] string modId,
                [FromServices] INexusModsApiClient apiClient,
                CancellationToken ct) =>
            {
                if (!RequestValidation.IsValidId(gameId) || !RequestValidation.IsValidId(modId))
                    return ShieldsResponseBody.Error("Version", "Invalid 'gameId' or 'modId'!");

                var response = await apiClient.GetModAsync(gameId, modId, ct);

                return response?.Version is not null
                    ? ShieldsResponseBody.Success("Version", response.Version)
                    : ShieldsResponseBody.Error("Version", "Invalid 'version' from NexusMods!");
            }).CacheOutput();
        }
    }
}