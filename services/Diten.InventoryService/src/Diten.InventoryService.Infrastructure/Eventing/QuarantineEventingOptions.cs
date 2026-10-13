namespace Diten.InventoryService.Infrastructure.Eventing;

/// <summary>
/// MOD-0175 Q2 — the <c>Eventing</c> section, the same shape as CrmService's <c>CrmEventingOptions</c>. Default
/// <c>InMemory</c> ⇒ MassTransit is NOT registered at all (nothing to connect to, nothing that can block start-up; RabbitMQ
/// does not run on this machine — read-time reconciliation is the working path). <c>Transport = "RabbitMQ"</c> with the
/// same broker as Platform wires <see cref="QuarantineWorkflowOutcomeConsumer"/>.
/// </summary>
public sealed class QuarantineEventingOptions
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
