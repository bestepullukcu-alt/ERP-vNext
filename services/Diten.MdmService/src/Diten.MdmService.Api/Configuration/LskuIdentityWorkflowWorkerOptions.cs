namespace Diten.MdmService.Api.Configuration;

public sealed class LskuIdentityWorkflowWorkerOptions
{
    public const string SectionName = "LskuIdentityWorkflowWorker";
    public bool Enabled { get; init; }
    public int PollIntervalSeconds { get; init; } = 15;
    public int TenantPageSize { get; init; } = 25;
    public int OperationPageSize { get; init; } = 25;
    public int LeaseSeconds { get; init; } = 60;
    public int RetryDelaySeconds { get; init; } = 30;
    public string LeaseOwner { get; init; } = string.Empty;

    public void EnsureValidWhenEnabled()
    {
        if (LeaseSeconds is < 10 or > 300 || RetryDelaySeconds is < 1 or > 3600)
            throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID");
        if (!Enabled) return;
        if (PollIntervalSeconds is < 1 or > 3600 || TenantPageSize is < 1 or > 100
            || OperationPageSize is < 1 or > 100 || string.IsNullOrWhiteSpace(LeaseOwner)
            || LeaseOwner.Length > 128 || LeaseOwner != LeaseOwner.Trim()
            || LeaseOwner.Any(char.IsControl))
            throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID");
    }
}
