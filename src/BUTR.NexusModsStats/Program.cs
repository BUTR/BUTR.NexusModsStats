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
const string OltpSectionName = "Oltp";

var builder = WebApplication.CreateSlimBuilder(args);

var connectionStringSection = builder.Configuration.GetSection(ConnectionStringsSectionName);

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
    var opts = connectionStringSection.Get<ConnectionStringsOptions>();

    options.ConnectionString = opts?.Main;
    options.SchemaName = "cache";
    options.TableName = "sitenexusmods_cache";
    options.CreateInfrastructure = true;
});

var openTelemetry = builder.Services.AddOpenTelemetry()
    .WithMetrics()
    .WithTracing()
    .WithLogging();

if (builder.Configuration.GetSection(OltpSectionName) is { } oltpSection)
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

    if (oltpSection.GetValue<string?>(nameof(OtlpOptions.MetricsEndpoint)) is { } metricsEndpoint)
    {
        var metricsProtocol = oltpSection.GetValue<OtlpExportProtocol>(nameof(OtlpOptions.MetricsProtocol));
        openTelemetry.WithMetrics(b => b
            .AddRuntimeInstrumentation(instrumentationOptions =>
            {

            })
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(metricsEndpoint);
                o.Protocol = metricsProtocol;
            }));
    }

    if (oltpSection.GetValue<string?>(nameof(OtlpOptions.TracingEndpoint)) is { } tracingEndpoint)
    {
        var tracingProtocol = oltpSection.GetValue<OtlpExportProtocol>(nameof(OtlpOptions.TracingProtocol));
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
                o.Endpoint = new Uri(tracingEndpoint);
                o.Protocol = tracingProtocol;
            }));
    }

    if (oltpSection.GetValue<string?>(nameof(OtlpOptions.LoggingEndpoint)) is { } loggingEndpoint)
    {
        var loggingProtocol = oltpSection.GetValue<OtlpExportProtocol>(nameof(OtlpOptions.LoggingProtocol));

        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeScopes = true;
            o.ParseStateValues = true;
            o.IncludeFormattedMessage = true;
            o.AddOtlpExporter((options, processorOptions) =>
            {
                options.Endpoint = new Uri(loggingEndpoint);
                options.Protocol = loggingProtocol;
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