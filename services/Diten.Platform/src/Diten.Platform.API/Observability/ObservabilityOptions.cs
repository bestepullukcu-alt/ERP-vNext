namespace Diten.Platform.API.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string ServiceName { get; set; } = "Diten.Platform";

    public string Environment { get; set; } = string.Empty;

    public SeqOptions Seq { get; set; } = new();

    public TracingOptions Tracing { get; set; } = new();

    public MetricsOptions Metrics { get; set; } = new();

    public HealthCheckOptions Health { get; set; } = new();

    public CorrelationOptions Correlation { get; set; } = new();

    public SensitiveDataRedactionOptions Redaction { get; set; } = new();
}

public sealed class SeqOptions
{
    public bool Enabled { get; set; }

    public string? Url { get; set; }

    public string? ApiKey { get; set; }

    public bool SafeDisableWhenUrlMissing { get; set; }
}

public sealed class TracingOptions
{
    public bool Enabled { get; set; } = true;

    public bool OtlpExporterEnabled { get; set; }

    public string? OtlpEndpoint { get; set; }

    public string DisabledReason { get; set; } = "OTLP exporter is disabled until local collector infrastructure is configured.";
}

public sealed class MetricsOptions
{
    public bool Enabled { get; set; } = true;

    public string Path { get; set; } = "/metrics";
}

public sealed class HealthCheckOptions
{
    public string Path { get; set; } = "/health";

    public string LivePath { get; set; } = "/health/live";

    public string ReadyPath { get; set; } = "/health/ready";
}

public sealed class CorrelationOptions
{
    public string HeaderName { get; set; } = "X-Correlation-Id";

    public int MaxLength { get; set; } = 128;
}

public sealed class SensitiveDataRedactionOptions
{
    public string RedactedText { get; set; } = "[REDACTED]";
}
