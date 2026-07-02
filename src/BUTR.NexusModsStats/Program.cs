using BUTR.NexusModsStats.Extensions;
using BUTR.NexusModsStats.Options;
using BUTR.NexusModsStats.Utils;

using Community.Microsoft.Extensions.Caching.PostgreSql;

using Npgsql;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateSlimBuilder(args);

const string ConnectionStringsSectionName = "ConnectionStrings";
var connectionStringSection = builder.Configuration.GetSection(ConnectionStringsSectionName);
builder.Services.Configure<ConnectionStringsOptions>(connectionStringSection);

const string NexusModsSectionName = "NexusMods";
var nexusModsSectionNameSection = builder.Configuration.GetSection(NexusModsSectionName);
builder.Services.Configure<NexusModsOptions>(nexusModsSectionNameSection);

const string OtlpSectionName = "Otlp";
var otlpSection = builder.Configuration.GetSection(OtlpSectionName);

const string UptimeKumaSectionName = "UptimeKuma";
var uptimeKumaSection = builder.Configuration.GetSection(UptimeKumaSectionName);
builder.Services.Configure<UptimeKumaOptions>(uptimeKumaSection);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ShieldsJsonSerializerContext.Default);
});
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(x => x.Expire(TimeSpan.FromSeconds(60)));
});
if (connectionStringSection.GetValue<string>(nameof(ConnectionStringsOptions.Main)) is { Length: > 0 } mainConnectionString)
{
    builder.Services.AddDistributedPostgreSqlCache(options =>
    {
        options.ConnectionString = mainConnectionString;
        options.SchemaName = "cache";
        options.TableName = "sitenexusmods_cache";
        options.CreateInfrastructure = true;
    });
}
else
{
    // No database configured (e.g. local development) - fall back to an in-process cache
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddSingleton<StripedAsyncLock>();

var openTelemetry = builder.Services.AddOpenTelemetry()
    .WithMetrics()
    .WithTracing()
    .WithLogging();

if (otlpSection.Get<OtlpOptions>() is { } otlpOptions)
{
    openTelemetry.ConfigureResource(b =>
    {
        b.AddService(
            builder.Environment.ApplicationName,
            builder.Environment.EnvironmentName,
            typeof(Program).Assembly.GetName().Version?.ToString(),
            false,
            Environment.MachineName);
        b.AddTelemetrySdk();
    });

    if (!string.IsNullOrEmpty(otlpOptions.MetricsEndpoint))
    {
        openTelemetry.WithMetrics(b => b
            .AddProcessInstrumentation()
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otlpOptions.MetricsEndpoint);
                o.Protocol = otlpOptions.MetricsProtocol;
            }));
    }

    if (!string.IsNullOrEmpty(otlpOptions.TracingEndpoint))
    {
        openTelemetry.WithTracing(b => b
            .AddNpgsql()
            .AddHttpClientInstrumentation(instrumentationOptions =>
            {
                instrumentationOptions.RecordException = true;
            })
            .AddAspNetCoreInstrumentation(instrumentationOptions =>
            {
                instrumentationOptions.RecordException = true;
            })
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otlpOptions.TracingEndpoint);
                o.Protocol = otlpOptions.TracingProtocol;
            }));
    }

    if (!string.IsNullOrEmpty(otlpOptions.LoggingEndpoint))
    {
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeScopes = true;
            o.ParseStateValues = true;
            o.IncludeFormattedMessage = true;
            o.AddOtlpExporter((options, processorOptions) =>
            {
                options.Endpoint = new Uri(otlpOptions.LoggingEndpoint);
                options.Protocol = otlpOptions.LoggingProtocol;
            });
        });
    }
}

var app = builder
    .AddDownloadsEndpoint()
    .AddModVersionEndpoint()
    .AddUptimeKuma()
    .Build();

app.UseOutputCache();
app.UseEndpointDefinitions();

app.Run();