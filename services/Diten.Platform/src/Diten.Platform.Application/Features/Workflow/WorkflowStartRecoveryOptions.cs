namespace Diten.Platform.Application.Features.Workflow;

/// <summary>
/// BL-549 — operator settings for the abandoned-start recovery (<see cref="BackgroundJobs.WorkflowStartRecoverySweepJob"/>).
/// </summary>
public sealed class WorkflowStartRecoveryOptions
{
    public const string SectionName = "Workflow:StartRecovery";

    public const int DefaultAbandonAfterMinutes = 30;

    /// <summary>The shortest age an operator can set (CT decision: 15 minutes). The age MUST exceed every consumer's wait
    /// window before it looks the key up again (MDM's StartSettleSeconds defaults to 120 s): a reservation is closed only
    /// once its consumer has stopped waiting for it. See the workflow-engine recovery runbook.</summary>
    public const int MinimumAbandonAfterMinutes = 15;

    /// <summary>How long a generic start may stand without its task (or a trusted start incomplete) before it counts as
    /// abandoned. Clamped up to <see cref="MinimumAbandonAfterMinutes"/>.</summary>
    public int AbandonAfterMinutes { get; set; } = DefaultAbandonAfterMinutes;

    /// <summary>How many abandoned starts one tenant's cycle closes at most; the rest wait for the next cycle.</summary>
    public int MaxClosesPerTenant { get; set; } = 100;

    /// <summary>FIX1 item 4 — while the recovery job is off, a read-only cycle still feeds the gauges.</summary>
    public bool CountWhileSweepIsOff { get; set; } = true;

    /// <summary>FIX1 item 4 · FIX2 item 4 — how often that read-only cycle runs: default 15 minutes, held between
    /// <see cref="MinimumCountIntervalMinutes"/> and <see cref="MaximumCountIntervalMinutes"/>. Every Platform replica runs
    /// its own (see the runbook: the cost is per replica).</summary>
    public int CountIntervalMinutes { get; set; } = DefaultCountIntervalMinutes;

    public const int DefaultCountIntervalMinutes = 15;
    public const int MinimumCountIntervalMinutes = 5;
    public const int MaximumCountIntervalMinutes = 24 * 60;

    /// <summary>FIX2 item 4 — the most unmoved Active instances one tenant's count looks at (every approval waiting longer
    /// than the age is one). Past it the gauges report "at least" (<c>count_capped</c> = 1). Held between 100 and 100 000.</summary>
    public int ScanLimitPerTenant { get; set; } = DefaultScanLimitPerTenant;

    public const int DefaultScanLimitPerTenant = 5_000;

    /// <summary>FIX2 K5 — after this many cycles in a row in which the same instance could not be written (503: no
    /// transaction), it is named once at Warning; before that, Info.</summary>
    public int DeferredWarnAfterCycles { get; set; } = 5;

    internal static TimeSpan CountIntervalOf(WorkflowStartRecoveryOptions? options) =>
        TimeSpan.FromMinutes(Math.Clamp(
            options?.CountIntervalMinutes ?? DefaultCountIntervalMinutes, MinimumCountIntervalMinutes, MaximumCountIntervalMinutes));

    internal static int ScanLimitOf(WorkflowStartRecoveryOptions? options) =>
        Math.Clamp(options?.ScanLimitPerTenant ?? DefaultScanLimitPerTenant, 100, 100_000);

    internal static TimeSpan AbandonAfterOf(WorkflowStartRecoveryOptions? options) =>
        TimeSpan.FromMinutes(Math.Max(MinimumAbandonAfterMinutes, options?.AbandonAfterMinutes ?? DefaultAbandonAfterMinutes));
}
