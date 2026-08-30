using Microsoft.Extensions.Options;

namespace Diten.PpmService.Infrastructure.Audit;


public sealed class PpmAuditProducerOptions
{
    public const string SectionName = "PpmAuditProducer";

    public bool Enabled { get; set; }
    public bool WorkerEnabled { get; set; }
    public string? KeyId { get; set; }
    public string? SecretBase64 { get; set; }
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 25;
    public int MaxAttempts { get; set; } = 5;
    public int InitialRetryDelaySeconds { get; set; } = 10;
    public int MaximumRetryDelaySeconds { get; set; } = 300;
    public int PublishingStaleAfterSeconds { get; set; } = 300;
    public string? RabbitMqHost { get; set; }
    public ushort RabbitMqPort { get; set; } = 5672;
    public string RabbitMqVirtualHost { get; set; } = "/";
    public string? RabbitMqUsername { get; set; }
    public string? RabbitMqPassword { get; set; }
}
