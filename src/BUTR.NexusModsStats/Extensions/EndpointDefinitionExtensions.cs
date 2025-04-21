using BUTR.NexusModsStats.Services;

namespace BUTR.NexusModsStats.Extensions;

public static class EndpointDefinitionExtensions
{
    public static WebApplication UseEndpointDefinitions(this WebApplication app)
    {
        var definitions = app.Services.GetRequiredService<IEnumerable<IEndpointDefinition>>();
        foreach (var def in definitions)
        {
            def.RegisterEndpoints(app);
        }
        return app;
    }
}