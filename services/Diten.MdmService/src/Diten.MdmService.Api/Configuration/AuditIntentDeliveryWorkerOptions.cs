namespace Diten.MdmService.Api.Configuration;

public sealed class AuditIntentDeliveryWorkerOptions
{
    public const string SectionName = "AuditIntentDeliveryWorker";
    public bool Enabled { get; init; }
    public int TenantPageSize { get; init; } = 25;
    public int BatchSize { get; init; } = 25;
    public int LeaseSeconds { get; init; } = 60;
    public int RetryDelaySeconds { get; init; } = 30;
    public int MaximumAttempts { get; init; } = 5;
    public int PollIntervalSeconds { get; init; } = 15;
    public string LeaseOwner { get; init; } = string.Empty;
}
