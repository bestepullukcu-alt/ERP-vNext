namespace Diten.Platform.Common.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string ServiceName { get; set; } = "Diten.Service";

    public string Environment { get; set; } = string.Empty;

    public SeqOptions Seq { get; set; } = new();

    public TracingOptions Tracing { get; set; } = new();

    public MetricsOptions Metrics { get; set; } = new();

    public HealthEndpointOptions Health { get; set; } = new();

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

public sealed class HealthEndpointOptions
{
    public string Path { get; set; } = "/health";

    public string LivePath { get; set; } = "/health/live";

    public string ReadyPath { get; set; } = "/health/ready";
}

public sealed class CorrelationOptions
{
    public string HeaderName { get; set; } = "X-Correlation-Id";

    public int MaxLength { get; set; } = 128;

    /// <summary>
    /// INTX FIX2 — whether an inbound <see cref="HeaderName"/> value is adopted as this request's correlation. True (the
    /// default, unchanged) for services behind the gateway: the value there is the gateway's own. The GATEWAY turns it
    /// off: at the edge the value is the caller's, and a caller must not be able to make its change look like part of
    /// another request (a response header shows every request's id) or gather a tenant's records under one id. The
    /// caller's value is not lost: it travels on <see cref="ClientHeaderName"/> and is recorded as what the client said.
    /// </summary>
    public bool TrustInboundCorrelation { get; set; } = true;

    /// <summary>The header that carries the CLIENT's own correlation value, kept apart from the server's.</summary>
    public string ClientHeaderName { get; set; } = "X-Client-Correlation-Id";
}

public sealed class SensitiveDataRedactionOptions
{
    public string RedactedText { get; set; } = "[REDACTED]";
}
