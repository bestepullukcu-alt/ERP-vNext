using Diten.MdmService.Application.Features.ItemMaster.Activation;

namespace Diten.MdmService.Api.Configuration;

// MOD-0290-FU04 S2: the item activation approval (flow ITEM_ACTIVATION). Off by default: a submit answers 503
// ITEM_ACTIVATION_APPROVAL_UNAVAILABLE and writes nothing. On: the template (by code or id) must exist as a published
// Platform definition, Platform's TrustedWorkflowStartAuthorization must list {Diten.MDM, TRUSTED_WORKFLOW_CONSUMER,
// Item, <code>}, and the approver is the QA Manager POSITION's id (OD-7) — data, never code.
public sealed class ItemActivationWorkflowOptions
{
    public const string SectionName = "ItemActivationWorkflow";
    public bool Enabled { get; init; }
    public Guid? TemplateId { get; init; }
    public string? TemplateCode { get; init; }
    public Guid ApproverPositionId { get; init; }
    public string ReasonCode { get; init; } = string.Empty;
    public bool CommentRequired { get; init; }
    public bool EvidenceRequired { get; init; }
    public int? DueAfterSeconds { get; init; }

    public ItemActivationWorkflowStartConfiguration ToStartConfiguration() => new(
        TemplateId, TemplateCode, ApproverPositionId, ReasonCode, CommentRequired, EvidenceRequired,
        DueAfterSeconds.HasValue ? TimeSpan.FromSeconds(DueAfterSeconds.Value) : null);

    public bool IsValid() => !Enabled
        || ItemActivationWorkflowStartRequestFactory.IsValid(ToStartConfiguration())
        && (!DueAfterSeconds.HasValue || DueAfterSeconds is >= 60 and <= 2_592_000);
}

public sealed class ItemActivationWorkflowWorkerOptions
{
    public const string SectionName = "ItemActivationWorkflowWorker";
    public bool Enabled { get; init; }
    public int PollIntervalSeconds { get; init; } = 15;
    public int TenantPageSize { get; init; } = 25;
    public int OperationPageSize { get; init; } = 25;
    public int LeaseSeconds { get; init; } = 60;
    public int RetryDelaySeconds { get; init; } = 30;
    // How long after a start the engine's "nothing arrived" is not trusted (1–600, the shared rule).
    public int StartSettleSeconds { get; init; } = 120;
    public string LeaseOwner { get; init; } = string.Empty;

    public void EnsureValidWhenEnabled()
    {
        if (LeaseSeconds is < 10 or > 300 || RetryDelaySeconds is < 1 or > 3600 || !StartSettleWindow.IsValid(StartSettleSeconds))
            throw new InvalidOperationException("ITEM_ACTIVATION_WORKFLOW_WORKER_CONFIGURATION_INVALID");
        if (!Enabled) return;
        if (PollIntervalSeconds is < 1 or > 3600 || TenantPageSize is < 1 or > 100 || OperationPageSize is < 1 or > 100
            || string.IsNullOrWhiteSpace(LeaseOwner) || LeaseOwner.Length > 128 || LeaseOwner != LeaseOwner.Trim()
            || LeaseOwner.Any(char.IsControl))
            throw new InvalidOperationException("ITEM_ACTIVATION_WORKFLOW_WORKER_CONFIGURATION_INVALID");
    }
}

// The item gauges (ITEM_ACTIVATION manual_reconciliation / needing_a_person, ITEM_ORPHAN_CODE): a read of counts, on
// by default, on their own worker — they never depend on the recovery worker being switched on.
public sealed class ItemActivationGaugeOptions
{
    public const string SectionName = "ItemActivationGauge";
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 300;
    // S2 FIX1 item 2: an approval the worker should be carrying, untouched this long, is counted as unprocessed. A running
    // worker touches every one at least once per retry ceiling (15 min, ProductIdentityRecoveryStreak.DefaultMaxRetryDelay)
    // plus its poll interval; 60 leaves a wide margin. Bounds 20–1440 (below 20 a healthy worker could be counted).
    public int UnprocessedAfterMinutes { get; set; } = 60;

    public TimeSpan UnprocessedAfter => TimeSpan.FromMinutes(Math.Clamp(UnprocessedAfterMinutes, 20, 1440));
}
