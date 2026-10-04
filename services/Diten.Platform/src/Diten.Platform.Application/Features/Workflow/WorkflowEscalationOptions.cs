namespace Diten.Platform.Application.Features.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 D2-FIX3 item 4 — operator settings for the SLA sweep (RunWorkflowEscalationsHandler).
/// </summary>
public sealed class WorkflowEscalationOptions
{
    public const string SectionName = "Workflow:Escalation";

    /// <summary>The default <see cref="StandingRest"/>: a day.</summary>
    public static readonly TimeSpan DefaultStandingRest = TimeSpan.FromHours(24);

    /// <summary>
    /// How long an overdue task rests when nothing the sweep could look at would change its answer: no SLA rule, an escalation
    /// with no timeout left to come, an instance that is gone or already closed. Never later than the task's next real SLA
    /// threshold. Before D2-FIX3 these came back every quarter of an hour and, past a few hundred per tenant, the five-minute
    /// sweep spent its pages on them while real escalations waited.
    /// </summary>
    public TimeSpan StandingRest { get; set; } = DefaultStandingRest;

    /// <summary>The configured rest, or the default when it is unset or shorter than the short recheck (a setting that would
    /// bring the backlog back).</summary>
    internal static TimeSpan StandingRestOf(WorkflowEscalationOptions? options, TimeSpan floor) =>
        options is { StandingRest: var rest } && rest >= floor ? rest : DefaultStandingRest;
}
