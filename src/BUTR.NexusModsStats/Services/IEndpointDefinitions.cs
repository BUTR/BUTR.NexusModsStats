using System.Diagnostics.CodeAnalysis;

namespace BUTR.NexusModsStats.Services;

public interface IEndpointDefinition
{
    [RequiresUnreferencedCode("Minimal API")]
    [RequiresDynamicCode("Minimal API")]
    void RegisterEndpoints(WebApplication app);
}