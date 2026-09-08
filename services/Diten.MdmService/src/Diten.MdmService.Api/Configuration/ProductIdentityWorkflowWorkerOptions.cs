namespace Diten.MdmService.Api.Configuration;

public sealed class ProductIdentityWorkflowWorkerOptions
{
    public const string SectionName = "ProductIdentityWorkflowWorker";

    public bool Enabled { get; init; }
    public int PollIntervalSeconds { get; init; } = 15;
    public int TenantPageSize { get; init; } = 25;
    public int OperationPageSize { get; init; } = 25;
    public int LeaseSeconds { get; init; } = 60;
    public int RetryDelaySeconds { get; init; } = 30;
    public string LeaseOwner { get; init; } = string.Empty;

    public void EnsureValidWhenEnabled()
    {
        if (!Enabled)
        {
            return;
        }

        if (PollIntervalSeconds is < 1 or > 3_600
            || TenantPageSize is < 1 or > 100
            || OperationPageSize is < 1 or > 100
            || LeaseSeconds is < 10 or > 900
            || RetryDelaySeconds is < 1 or > 3_600
            || string.IsNullOrWhiteSpace(LeaseOwner)
            || LeaseOwner.Length > 128
            || !string.Equals(LeaseOwner, LeaseOwner.Trim(), StringComparison.Ordinal)
            || LeaseOwner.Any(char.IsControl))
        {
            throw new InvalidOperationException("PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID");
        }
    }
}
