using BUTR.NexusModsStats.Options;
using BUTR.NexusModsStats.Services;

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
        var assemblyName = typeof(DownloadsExtensions).Assembly.GetName();
        var userAgent = $"{assemblyName.Name ?? "ERROR"} v{assemblyName.Version?.ToString() ?? "ERROR"} (github.com/BUTR)";

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IEndpointDefinition, UptimeKumaEndpointDefinition>());
        builder.Services.AddHttpClient<IHealthCheckPublisher, UptimeKumaHealthCheckPublisher>().ConfigureHttpClient((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<UptimeKumaOptions>>().Value;
            
            if (Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri))
                client.BaseAddress = uri;
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        builder.Services.AddHttpClient<NexusModsApiHealthCheck>().ConfigureHttpClient((_, client) =>
        {
            client.BaseAddress = new Uri("https://nexusmods.statuspage.io/");
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        builder.Services.AddHealthChecks()
            .AddCheck<NexusModsApiHealthCheck>("NexusModsApi");

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
    
    public partial class NexusModsApiHealthCheck : IHealthCheck
    {
        [JsonSerializable(typeof(Components))]
        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        public partial class NexusModsStatusJsonSerializerContext : JsonSerializerContext;
        
        public record Component(
            [property: JsonPropertyName("id")] string id,
            [property: JsonPropertyName("name")] string name,
            [property: JsonPropertyName("status")] string status,
            [property: JsonPropertyName("created_at")] DateTime created_at,
            [property: JsonPropertyName("updated_at")] DateTime updated_at,
            [property: JsonPropertyName("position")] int position,
            [property: JsonPropertyName("description")] string description,
            [property: JsonPropertyName("showcase")] bool showcase,
            [property: JsonPropertyName("start_date")] string start_date,
            [property: JsonPropertyName("group_id")] object group_id,
            [property: JsonPropertyName("page_id")] string page_id,
            [property: JsonPropertyName("group")] bool group,
            [property: JsonPropertyName("only_show_if_degraded")] bool only_show_if_degraded
        );

        public record Page(
            [property: JsonPropertyName("id")] string id,
            [property: JsonPropertyName("name")] string name,
            [property: JsonPropertyName("url")] string url,
            [property: JsonPropertyName("time_zone")] string time_zone,
            [property: JsonPropertyName("updated_at")] DateTime updated_at
        );

        public record Components(
            [property: JsonPropertyName("page")] Page page,
            [property: JsonPropertyName("components")] IReadOnlyList<Component> components
        );
        
        private readonly HttpClient _httpClient;

        public NexusModsApiHealthCheck(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            var response = await _httpClient.GetFromJsonAsync("api/v2/components.json", NexusModsStatusJsonSerializerContext.Default.Components, ct);
            var apiComponent = response?.components.FirstOrDefault(x => x.name == "API");
            if (apiComponent is null)
                return HealthCheckResult.Unhealthy("NexusMods API Status not available");

            if (apiComponent.status != "operational")
                return HealthCheckResult.Degraded($"NexusMods API Status is {apiComponent.status}");

            return HealthCheckResult.Healthy();
        }
    }
}