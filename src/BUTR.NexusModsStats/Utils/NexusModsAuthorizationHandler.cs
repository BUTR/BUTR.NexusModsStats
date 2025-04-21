using BUTR.NexusModsStats.Options;

using Microsoft.Extensions.Options;

namespace BUTR.NexusModsStats.Utils;

public sealed class NexusModsAuthorizationHandler : DelegatingHandler
{
    private readonly NexusModsOptions _options;

    public NexusModsAuthorizationHandler(IOptionsSnapshot<NexusModsOptions> options)
    {
        _options = options.Value;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("apikey", _options.ApiKey);

        return base.Send(request, cancellationToken);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("apikey", _options.ApiKey);

        return base.SendAsync(request, cancellationToken);
    }
}