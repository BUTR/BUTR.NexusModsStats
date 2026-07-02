using BUTR.NexusModsStats.Services;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BUTR.NexusModsStats.Extensions;

public static class VersionExtensions
{
    public static WebApplicationBuilder AddVersionEndpoint(this WebApplicationBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IEndpointDefinition, VersionEndpointDefinition>());
        return builder;
    }

    public class VersionEndpointDefinition : IEndpointDefinition
    {
        public void RegisterEndpoints(WebApplication app)
        {
            // Returns the image tag baked in at build time; the deploy workflow polls this
            // to confirm the new build is actually the one serving traffic, so it must not be cached
            app.MapGet("/version", static () =>
                    Results.Text(Environment.GetEnvironmentVariable("APP_VERSION") ?? "unknown"))
                .CacheOutput(static x => x.NoCache());
        }
    }
}