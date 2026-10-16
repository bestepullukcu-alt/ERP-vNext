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
    // S2 FIX1 item 2 / FIX2 item 2 / FIX3 item 2: an approval the worker should be carrying with no real move (ProgressAt)
    // this long is counted as unprocessed. A running worker moves every one at least once per its longest planned wait —
    // the configuration retry (ConfigurationRetryDelay, 1 h; the decision poll's ceiling is 15 min) — plus its poll
    // interval and lease: 60 min + 15 s + 60 s by default; 90 leaves ~29 minutes. Validated at start against the worker's
    // own settings (never clamped silently): 20–1440 and above the longest wait + poll interval + lease.
    // S2 lane merge (minor): the default is derived, not typed — one and a half times the longest planned wait (90 today).
    public int UnprocessedAfterMinutes { get; set; } = DefaultUnprocessedAfterMinutes;

    public static int DefaultUnprocessedAfterMinutes => (int)Math.Ceiling(LongestPlannedWait.TotalMinutes * 1.5);

    public TimeSpan UnprocessedAfter => TimeSpan.FromMinutes(UnprocessedAfterMinutes);

    public bool IsValid(ItemActivationWorkflowWorkerOptions worker) =>
        UnprocessedAfterMinutes is >= 20 and <= 1440
        && UnprocessedAfter > LongestPlannedWaitFor(worker)
            + TimeSpan.FromSeconds(Math.Max(worker.PollIntervalSeconds, 0)) + TimeSpan.FromSeconds(Math.Max(worker.LeaseSeconds, 0));

    // The longest a healthy worker leaves an operation untouched on purpose.
    public static TimeSpan LongestPlannedWait =>
        ItemActivationWorkflowProcessor.ConfigurationRetryDelay > ItemActivationWorkflowProcessor.DecisionPollCeiling
            ? ItemActivationWorkflowProcessor.ConfigurationRetryDelay
            : ItemActivationWorkflowProcessor.DecisionPollCeiling;

    // S2 lane merge (minor): the worker's own retry delay is a planned wait too (a decision poll takes it when it is above
    // the ceiling). With today's bound (≤ 3600 s) it never exceeds the configuration retry; it is counted so a wider bound
    // cannot slip past the threshold.
    public static TimeSpan LongestPlannedWaitFor(ItemActivationWorkflowWorkerOptions worker)
    {
        var retry = TimeSpan.FromSeconds(Math.Max(worker.RetryDelaySeconds, 0));
        return retry > LongestPlannedWait ? retry : LongestPlannedWait;
    }

    public const string ConfigurationInvalid = "ITEM_ACTIVATION_GAUGE_CONFIGURATION_INVALID";
}
