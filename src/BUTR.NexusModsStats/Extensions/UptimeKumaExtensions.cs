using BUTR.NexusModsStats.Options;
using BUTR.NexusModsStats.Services;
using BUTR.NexusModsStats.Utils;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using System.Globalization;
using System.Text.Json.Serialization;

namespace BUTR.NexusModsStats.Extensions;

public static partial class UptimeKumaExtensions
{
    public static WebApplicationBuilder AddUptimeKuma(this WebApplicationBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IEndpointDefinition, UptimeKumaEndpointDefinition>());
        builder.Services.AddHttpClient<IHealthCheckPublisher, UptimeKumaHealthCheckPublisher>().ConfigureHttpClient((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<UptimeKumaOptions>>().Value;

            if (Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri))
                client.BaseAddress = uri;
            client.DefaultRequestHeaders.Add("User-Agent", HttpUtils.UserAgent);
        });
        builder.Services.AddHttpClient<NexusModsApiHealthCheck>().ConfigureHttpClient((_, client) =>
        {
            client.BaseAddress = new Uri("https://nexusmods.statuspage.io/");
            client.DefaultRequestHeaders.Add("User-Agent", HttpUtils.UserAgent);
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        builder.Services.AddHealthChecks()
            .AddCheck<NexusModsApiHealthCheck>("NexusModsApi")
            .AddCheck<DistributedCacheHealthCheck>("Cache");

        return builder;
    }

    public sealed class UptimeKumaHealthCheckPublisher : IHealthCheckPublisher
    {
        private readonly HttpClient _httpClient;

        public UptimeKumaHealthCheckPublisher(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task PublishAsync(HealthReport report, CancellationToken ct)
        {
            if (_httpClient.BaseAddress is null)
                return;

            var status = report.Status switch
            {
                HealthStatus.Unhealthy => "down",
                HealthStatus.Degraded => "pending",
                HealthStatus.Healthy => "up",
                _ => throw new ArgumentOutOfRangeException(),
            };
            var message = string.Join(", ", report.Entries.Select(x => $"{x.Key} - {x.Value.Description}"));
            var ping = report.TotalDuration.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            var response = await _httpClient.GetAsync($"?status={status}&msg={Uri.EscapeDataString(message)}&ping={ping}", ct);
            response.EnsureSuccessStatusCode();
        }
    }

    public class UptimeKumaEndpointDefinition : IEndpointDefinition
    {
        public void RegisterEndpoints(WebApplication app)
        {
            app.MapHealthChecks("/healthz");
        }
    }

    public sealed class DistributedCacheHealthCheck : IHealthCheck
    {
        private static readonly DistributedCacheEntryOptions Expiration = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };

        private readonly IDistributedCache _cache;

        public DistributedCacheHealthCheck(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                await _cache.SetStringAsync("healthz", "ok", Expiration, ct);
                return HealthCheckResult.Healthy();
            }
            catch (Exception e)
            {
                return HealthCheckResult.Unhealthy("Distributed cache is not available", e);
            }
        }
    }

    public partial class NexusModsApiHealthCheck : IHealthCheck
    {
        [JsonSerializable(typeof(Components))]
        public partial class NexusModsStatusJsonSerializerContext : JsonSerializerContext;

        // Only the fields that are actually used are declared - extra JSON properties are ignored,
        // and loosely-typed members (e.g. 'object group_id') would break AOT serialization
        public record Component(
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("status")] string Status
        );

        public record Components(
            [property: JsonPropertyName("components")] IReadOnlyList<Component> ComponentList
        );

        private readonly HttpClient _httpClient;

        public NexusModsApiHealthCheck(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync("api/v2/components.json", NexusModsStatusJsonSerializerContext.Default.Components, ct);
                var apiComponent = response?.ComponentList.FirstOrDefault(x => x.Name == "API");
                if (apiComponent is null)
                    return HealthCheckResult.Unhealthy("NexusMods API Status not available");

                if (apiComponent.Status != "operational")
                    return HealthCheckResult.Degraded($"NexusMods API Status is {apiComponent.Status}");

                return HealthCheckResult.Healthy();
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                return HealthCheckResult.Unhealthy("NexusMods API Status not available", e);
            }
        }
    }
}