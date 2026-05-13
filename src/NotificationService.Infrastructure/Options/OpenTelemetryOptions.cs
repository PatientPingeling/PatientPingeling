using OpenTelemetry.Exporter;

namespace NotificationService.Infrastructure.Options;

public sealed class OpenTelemetryOptions
{
  public const string SectionName = "OpenTelemetry";

  // Service identity — ServiceName is passed per-service to AddOpenTelemetry(), not read from config
  public string ServiceVersion { get; init; } = "1.0.0";
  public string Environment { get; init; } = "Development";

  // Transport
  public string Endpoint { get; init; } = "http://localhost:4317";

  public OtlpExportProtocol Protocol { get; init; } = OtlpExportProtocol.Grpc;

  // Auth headers e.g. "Authorization": "Bearer xyz"
  public Dictionary<string, string> Headers { get; init; } = [];

  // Signal toggles
  public bool EnableTracing { get; init; } = true;
  public bool EnableMetrics { get; init; } = true;
  public bool EnableLogging { get; init; } = true;

  // Sampling (0.0 - 1.0)
  public double SamplingRatio { get; init; } = 1.0;

  // Export timeout
  public TimeSpan ExportTimeout { get; init; } = TimeSpan.FromSeconds(10);
}