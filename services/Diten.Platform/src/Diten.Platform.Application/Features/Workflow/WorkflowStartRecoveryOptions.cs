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

    internal static TimeSpan AbandonAfterOf(WorkflowStartRecoveryOptions? options) =>
        TimeSpan.FromMinutes(Math.Max(MinimumAbandonAfterMinutes, options?.AbandonAfterMinutes ?? DefaultAbandonAfterMinutes));
}
