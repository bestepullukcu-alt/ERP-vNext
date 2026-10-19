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
    // LSKU FIX2 item 10: how long after a start the engine's "nothing arrived" is not trusted.
    public int StartSettleSeconds { get; init; } = 120;
    public string LeaseOwner { get; init; } = string.Empty;
    // BL-607: an approval open longer than this is worth an alert (the open_approval.oldest_age gauge against
    // open_approval.alert_after). Default one day. It must be longer than the open-approval backoff ceiling plus one poll
    // interval — the longest a DECIDED approval can still look open — or the alert would fire on a healthy approval; a
    // threshold that is not is refused at start (ValidateOnStart), never quietly raised.
    public int OpenApprovalAlertAfterMinutes { get; init; } = 1440;

    public bool OpenApprovalAlertThresholdIsValid() =>
        OpenApprovalAlertAfterMinutes is >= 1 and <= 525600
        && TimeSpan.FromMinutes(OpenApprovalAlertAfterMinutes)
            > Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.ProductIdentityOpenApprovalBackoff
                  .Ceiling(TimeSpan.FromSeconds(RetryDelaySeconds))
              + TimeSpan.FromSeconds(PollIntervalSeconds);

    public void EnsureValidWhenEnabled()
    {
        if (LeaseSeconds is < 10 or > 300 || RetryDelaySeconds is < 1 or > 3600 || !StartSettleWindow.IsValid(StartSettleSeconds))
            throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID");
        if (!Enabled) return;
        if (PollIntervalSeconds is < 1 or > 3600 || TenantPageSize is < 1 or > 100
            || OperationPageSize is < 1 or > 100 || string.IsNullOrWhiteSpace(LeaseOwner)
            || LeaseOwner.Length > 128 || LeaseOwner != LeaseOwner.Trim()
            || LeaseOwner.Any(char.IsControl))
            throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID");
        if (!OpenApprovalAlertThresholdIsValid())
            throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_OPEN_APPROVAL_ALERT_THRESHOLD_INVALID");
    }
}
