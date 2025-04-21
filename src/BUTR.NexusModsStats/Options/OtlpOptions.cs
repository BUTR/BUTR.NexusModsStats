using OpenTelemetry.Exporter;

namespace BUTR.NexusModsStats.Options;

public sealed record OtlpOptions
{
    public string LoggingEndpoint { get; init; } = null!;
    public OtlpExportProtocol LoggingProtocol { get; init; }
    public string TracingEndpoint { get; init; } = null!;
    public OtlpExportProtocol TracingProtocol { get; init; }
    public string MetricsEndpoint { get; init; } = null!;
    public OtlpExportProtocol MetricsProtocol { get; init; }
}