using OpenTelemetry.Exporter;

namespace BUTR.NexusModsStats.Options;

public sealed record OtlpOptions
{
    public string LoggingEndpoint { get; set; } = null!;
    public OtlpExportProtocol LoggingProtocol { get; set; }
    public string TracingEndpoint { get; set; } = null!;
    public OtlpExportProtocol TracingProtocol { get; set; }
    public string MetricsEndpoint { get; set; } = null!;
    public OtlpExportProtocol MetricsProtocol { get; set; }
}