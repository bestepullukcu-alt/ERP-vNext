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
    // A record waiting on a fault of the service chain backs off up to this ceiling, and its waiting is logged as an
    // error from this many consecutive attempts on. Read by the worker AND by the interactive paths.
    public int ServiceFaultMaxRetryDelaySeconds { get; init; } = 900;
    public int ServiceFaultErrorAfterAttempts { get; init; } = 5;

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
            || !IsServiceFaultWindowValid(
                RetryDelaySeconds, ServiceFaultMaxRetryDelaySeconds, ServiceFaultErrorAfterAttempts)
            || string.IsNullOrWhiteSpace(LeaseOwner)
            || LeaseOwner.Length > 128
            || !string.Equals(LeaseOwner, LeaseOwner.Trim(), StringComparison.Ordinal)
            || LeaseOwner.Any(char.IsControl))
        {
            throw new InvalidOperationException("PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID");
        }
    }

    // The ceiling may not be shorter than the ordinary retry delay: a "back-off" that is flat and quicker than the
    // normal poll is not one.
    public static bool IsServiceFaultWindowValid(int retryDelaySeconds, int maxRetryDelaySeconds, int errorAfterAttempts) =>
        maxRetryDelaySeconds is >= 1 and <= 86_400
        && errorAfterAttempts is >= 1 and <= 1_000
        && retryDelaySeconds <= maxRetryDelaySeconds;
}
