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

    /// <summary>D2-FIX4 item 7 — the longest standing rest an operator can set: a week. A task nothing will change is still
    /// looked at that often, so a typo (days for hours) cannot park a backlog out of sight for good.</summary>
    public static readonly TimeSpan MaxStandingRest = TimeSpan.FromDays(7);

    /// <summary>
    /// The configured rest, held between <paramref name="floor"/> (the short recheck — anything shorter would bring the
    /// backlog back) and <see cref="MaxStandingRest"/>; the default when there are no options at all.
    ///
    /// <para>D2-FIX4 item 7 — a value below the floor used to become the DEFAULT (a day) without a word: an operator who
    /// asked for ten minutes got twenty-four hours, the opposite end of the range. It is now CLAMPED to the floor, and the
    /// ceiling is clamped the same way, so the setting always lands on the nearest value the sweep allows.</para>
    /// </summary>
    internal static TimeSpan StandingRestOf(WorkflowEscalationOptions? options, TimeSpan floor)
    {
        if (options is null)
        {
            return DefaultStandingRest;
        }

        var rest = options.StandingRest;
        return rest < floor ? floor : rest > MaxStandingRest ? MaxStandingRest : rest;
    }
}
