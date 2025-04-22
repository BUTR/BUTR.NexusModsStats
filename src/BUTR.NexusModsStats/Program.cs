using BUTR.NexusModsStats.Extensions;
using BUTR.NexusModsStats.Options;
using BUTR.NexusModsStats.Utils;

using Community.Microsoft.Extensions.Caching.PostgreSql;

using Npgsql;

using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string ConnectionStringsSectionName = "ConnectionStrings";
const string NexusModsSectionName = "NexusMods";
const string OtlpSectionName = "Otlp";

var builder = WebApplication.CreateSlimBuilder(args);

var connectionStringSection = builder.Configuration.GetSection(ConnectionStringsSectionName);
builder.Services.Configure<ConnectionStringsOptions>(connectionStringSection);

var nexusModsSectionNameSection = builder.Configuration.GetSection(NexusModsSectionName);
builder.Services.Configure<NexusModsOptions>(nexusModsSectionNameSection);

var otlpSection = builder.Configuration.GetSection(OtlpSectionName);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ShieldsJsonSerializerContext.Default);
});
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(x => x.Expire(TimeSpan.FromSeconds(60)));
});
builder.Services.AddDistributedPostgreSqlCache(options =>
{
    var main = connectionStringSection.GetValue<string>(nameof(ConnectionStringsOptions.Main));

    options.ConnectionString = main;
    options.SchemaName = "cache";
    options.TableName = "sitenexusmods_cache";
    options.CreateInfrastructure = true;
});

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
            .AddRuntimeInstrumentation(instrumentationOptions =>
            {

            })
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
    .Build()
    .UseEndpointDefinitions();

app.Run();