namespace Diten.MdmService.Infrastructure.Eventing;

/// <summary>
/// WP-VW-W4 (W4-BE-b1) — the MDM eventing transport options (the CrmService <c>CrmEventingOptions</c> copy, same
/// <c>Eventing</c> section shape). Default <c>InMemory</c> ⇒ MassTransit is NOT registered at all (nothing to connect to,
/// nothing that can block start-up); <c>Transport = "RabbitMQ"</c> with the same broker as Platform wires the workflow
/// outcome consumer.
/// </summary>
public sealed class MdmEventingOptions
{
    public const string SectionName = "Eventing";

    public string Transport { get; set; } = "InMemory";

    public int RetryCount { get; set; } = 5;
    public int InitialRetryDelaySeconds { get; set; } = 10;
    public int MaxRetryDelaySeconds { get; set; } = 300;

    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public bool UseTls { get; set; }

    public bool UseRabbitMq => string.Equals(Transport, "RabbitMQ", StringComparison.OrdinalIgnoreCase);
}
