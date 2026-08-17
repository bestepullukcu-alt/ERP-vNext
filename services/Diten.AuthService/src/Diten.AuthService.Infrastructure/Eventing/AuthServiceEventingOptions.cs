namespace Diten.AuthService.Infrastructure.Eventing;

/// <summary>
/// AuthService eventing transport options — same <c>Eventing</c> section shape as Platform's
/// RabbitMqEventingOptions. Default <c>InMemory</c> means the cross-service consumer is NOT wired
/// (in-memory does not cross process boundaries). Cross-service entitlement sync requires
/// <c>Transport = "RabbitMQ"</c> with the same broker as Platform (a deploy/config step — see S18).
/// </summary>
public sealed class AuthServiceEventingOptions
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
