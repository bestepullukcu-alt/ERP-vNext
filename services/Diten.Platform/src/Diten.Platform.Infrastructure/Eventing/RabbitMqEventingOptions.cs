namespace Diten.Platform.Infrastructure.Eventing;

public sealed class RabbitMqEventingOptions
{
    public const string SectionName = "Eventing";

    public string Transport { get; set; } = "InMemory";

    public bool WorkerEnabled { get; set; } = true;

    public int BatchSize { get; set; } = 25;

    public int PollIntervalSeconds { get; set; } = 5;

    public int RetryCount { get; set; } = 5;

    public int InitialRetryDelaySeconds { get; set; } = 10;

    public int MaxRetryDelaySeconds { get; set; } = 300;

    public int DeadLetterRetentionDays { get; set; } = 30;

    public string Host { get; set; } = "localhost";

    public ushort Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public bool UseTls { get; set; }

    public bool UseRabbitMq => string.Equals(Transport, "RabbitMQ", StringComparison.OrdinalIgnoreCase);
}
