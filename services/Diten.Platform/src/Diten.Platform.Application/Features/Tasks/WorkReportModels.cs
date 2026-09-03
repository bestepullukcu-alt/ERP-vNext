using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;

namespace Diten.Platform.Application.Features.Tasks;

/// <summary>
/// MOD-0024 Faz 5a — the WORK REPORT: how work is flowing, for the work the caller may see.
///
/// <para><b>No new data is collected.</b> Every measure below is derived from something the engine has recorded
/// all along: <c>CreatedAt</c>, <c>CompletedAt</c>/<c>CancelledAt</c>, <c>DueAt</c>, <c>EstimateHours</c>,
/// <c>SpentHours</c>, <c>ClosureReasonCode</c> (Faz 3) and <c>TaskTransitionKind.Returned</c> (Faz 4).</para>
///
/// <para><b>⚠ THERE IS NO PERSONAL PRODUCTIVITY SCORE HERE, and its absence is a decision.</b> Oracle's worklist
/// report set is Unattended · Priority · Cycle Time · Productivity · Time Distribution, and its PRODUCTIVITY
/// report is the COUNT of tasks assigned versus completed in a period — not a percentage against an estimate.
/// Estimate-versus-actual is reported as a PLAN-QUALITY signal, grouped by type or unit; turning it into a
/// per-person score makes people inflate estimates, which corrupts the only planning input the system has. Pack
/// §8 excludes it with the same reasoning.</para>
/// </summary>
/// <remarks>
/// ⚠ STRING ON THE WIRE. It arrives as a query-string value and travels back inside the report, and an enum that
/// reaches a client as a NUMBER is a defect this module has already shipped twice — <c>TaskJsonContractTests</c>
/// caught this one before it left the branch.
/// </remarks>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum WorkReportGroupBy
{
    /// <summary>Totals only.</summary>
    None = 0,
    TaskType = 1,
    OrganizationUnit = 2,
    Assignee = 3,
    Priority = 4,

    /// <summary>
    /// WHICH COMPANY the work belongs to.
    ///
    /// <para>⚠ A DERIVED AXIS, and the only one. MEASURED 2026-09-04: <c>TaskItem</c> carries no legal-entity
    /// field — it carries <c>OrganizationUnitId</c> (<c>required Guid</c>), and the company hangs off the unit
    /// (<c>OrganizationUnit.LegalEntityId</c>, itself <c>required</c>). So this axis is a JOIN the repository
    /// resolves, not a column it reads.</para>
    ///
    /// <para>A task whose unit cannot be resolved — deleted, archived out of the tenant's live set, or an id
    /// pointing at nothing — lands in <see cref="WorkReportDto.UnassignedKey"/> rather than being dropped. A
    /// silently discarded row makes the groups fail to add up to the totals, and nobody ever finds out why.</para>
    /// </summary>
    LegalEntity = 5
}

/// <summary>
/// WHAT to count, already narrowed to what the caller may see.
///
/// <para><b>The period is REQUIRED, and that is a load-bearing limit rather than a formality.</b> The only broad
/// read this repository had was <c>GetAllForTenantAsync</c> — fine at today's volume, ruinous at a hundred
/// thousand tasks. A report with no period is a full-collection scan wearing a date picker, so the contract
/// refuses one.</para>
///
/// <para>Built by the handler from the resolved <see cref="WorkReportScope"/> and handed to the repository,
/// which turns it into ONE database aggregation. It carries no Mongo type, so the criteria stay testable
/// without a database and the translation stays in one place.</para>
/// </summary>
/// <param name="From">Inclusive start of the period.</param>
/// <param name="To">EXCLUSIVE end. Half-open like every other interval in this codebase, so consecutive periods
/// cannot both claim the same day.
///
/// <para>⚠ The wording here deliberately does NOT name the position assignment's effective-date columns.
/// <c>TaskSeatDirectoryTests</c> treats any mention of them under <c>Features/Tasks</c> as a file deciding for
/// itself what "currently holds this seat" means — a rule that has to be re-reasoned when HCM takes the columns
/// over. This file decides nothing of the sort; citing them as an analogy would have added a false site to that
/// search, and the guard was right to say so.</para></param>
/// <param name="Filter">
/// Optional narrowing, applied AFTER the scope. See <see cref="WorkReportFilter"/> for why the order is the
/// whole rule.
/// </param>
public sealed record WorkReportCriteria(
    DateTimeOffset From,
    DateTimeOffset To,
    WorkReportScope Scope,
    WorkReportGroupBy GroupBy = WorkReportGroupBy.None,
    WorkReportFilter? Filter = null,
    /// <summary>
    /// Whether to measure the preceding period of the same length as well. Off by default so the comparison is
    /// paid for only when it is asked for — it doubles the reads.
    /// </summary>
    bool ComparePrevious = false);

/// <summary>
/// WHICH SLICE of the work the reader asked about — every field optional, and none of them a substitute for the
/// scope.
///
/// <para><b>⚠ A FILTER INTERSECTS THE SCOPE; IT NEVER REPLACES IT.</b> Naming a person outside your data scope
/// in <see cref="AssigneeUserId"/> returns an EMPTY report, not theirs. The same for a unit, a company or a
/// type. The order is fixed and load-bearing: the scope narrows first, the filter narrows further, and there is
/// no path in which a filter widens anything. Reversing the two would turn a reporting parameter into a
/// privilege-escalation seam — a query string that reads other people's work.</para>
///
/// <para><b>All-null is the identity.</b> A criteria set with no filter must produce exactly what the report
/// produced before filters existed; that is asserted rather than assumed, because "additive" is a claim about
/// the past that only a test can keep true.</para>
/// </summary>
/// <param name="LegalEntityId">
/// The company. DERIVED through the unit, exactly as the <see cref="WorkReportGroupBy.LegalEntity"/> axis is —
/// a task carries no company of its own.
/// </param>
/// <param name="OrganizationUnitId">
/// One unit, matched EXACTLY rather than as a subtree. The scope already carries a pre-expanded subtree from
/// the resolver; expanding again here would be a second walk of the same tree, and the two would eventually
/// disagree about what "below me" means. A reader who wants a subtree picks the parent's scope, not this.
/// </param>
/// <param name="TaskTypeCode">
/// The type's CODE, not its id — a code is what a person reads aloud and types into a link, and it is stable by
/// contract (<c>TaskType.Code</c> is immutable once created).
/// </param>
public sealed record WorkReportFilter(
    Guid? LegalEntityId = null,
    Guid? OrganizationUnitId = null,
    Guid? AssigneeUserId = null,
    string? TaskTypeCode = null,
    TaskPriority? Priority = null)
{
    /// <summary>Nothing was asked for — the report is exactly the unfiltered one.</summary>
    public bool IsEmpty =>
        LegalEntityId is null
        && OrganizationUnitId is null
        && AssigneeUserId is null
        && string.IsNullOrWhiteSpace(TaskTypeCode)
        && Priority is null;
}

/// <summary>One measured slice of the period — the totals, or one group of them.</summary>
/// <param name="Key">Null for the totals row; otherwise the group's identity (a type id, unit id, user id or
/// priority name) as a string, so one shape serves every axis.</param>
/// <param name="Label">
/// The words for <paramref name="Key"/>, when the server can supply them — a type's name, a unit's name, a
/// company's name.
///
/// <para><b>NULL is a real answer, and never a fabricated one.</b> It stays null for the ASSIGNEE axis, because
/// MEASURED 2026-09-04 there is no User entity in Platform and no auth client to ask — the screen resolves
/// people through the lookup it already uses. It also stays null when a name genuinely cannot be found (a unit
/// deleted since the work was done), and the screen then shows the identity. Inventing a placeholder would put a
/// label on screen that matches nothing anybody could search for.</para>
/// </param>
/// <param name="CycleTime">
/// How long COMPLETED work took. ⚠ Completions only — see <paramref name="CancellationTime"/>.
/// </param>
/// <param name="CancellationTime">
/// How long CALLED-OFF work sat before somebody called it off.
///
/// <para><b>⚠ SPLIT OUT, NOT DELETED, AND THE SPLIT IS THE POINT.</b> MEASURED 2026-09-04: cycle time counted
/// completions and cancellations together, so a task that waited ninety days and was then abandoned was
/// reported as ninety days of "how long our work takes". Oracle measures cycle time over completions alone,
/// and the reason is that the two answer different questions: one is "how fast do we finish", the other is
/// "how long before we admit we won't". Both are worth knowing; averaged together they are worth nothing.</para>
/// </param>
/// <param name="Aging">Open work at the period's end, by age. See <see cref="WorkReportAging"/>.</param>
public sealed record WorkReportBucket(
    string? Key,
    string? Label,
    WorkReportFlow Flow,
    WorkReportDuration CycleTime,
    WorkReportDuration CancellationTime,
    WorkReportAging Aging,
    WorkReportTimeliness Timeliness,
    WorkReportEffort Effort,
    IReadOnlyList<WorkReportOutcomeCount> Outcomes,
    WorkReportRework Rework);

/// <summary>
/// HOW MUCH WORK MOVED — Oracle's Productivity, as counts.
/// </summary>
/// <param name="Opened">Tasks created within the period.</param>
/// <param name="Closed">Tasks that reached a terminal state within the period. <c>Completed + Cancelled</c>.</param>
/// <param name="Completed">Closed as done.</param>
/// <param name="Cancelled">Called off.</param>
/// <param name="Unattended">
/// Oracle's Unattended: open work nobody is holding. Counted at the moment the report runs rather than over the
/// period — "how much is sitting unclaimed" is a question about now, and a historical answer would need a
/// per-day reconstruction this slice does not do.
/// </param>
public sealed record WorkReportFlow(int Opened, int Closed, int Completed, int Cancelled, int Unattended);

/// <summary>
/// HOW LONG SOMETHING TOOK, from creation to the moment it ended — in days.
///
/// <para><b>⚠ AVERAGE AND MEDIAN TOGETHER, and never one alone.</b> Durations are skewed: one task parked for
/// ninety days drags an average nobody recognises. <c>1 · 2 · 3 · 4 · 90</c> averages to 20 and has a median of
/// 3, and the pair is the finding — a large gap between them says "there is a tail", which is the thing worth
/// acting on. Either number by itself invites the wrong decision.</para>
///
/// <para><b>MEDIAN IS A DISTRIBUTION MEASURE, NOT A PERFORMANCE SCORE.</b> It is reported for a period, a type
/// or a unit — never turned into a ranking of people. Pack §8's exclusion of efficiency scoring covers this for
/// the same reason: a number that becomes a personal score changes the behaviour it was measuring.</para>
///
/// <para><b><see cref="Count"/> is the DENOMINATOR THE AVERAGE WAS ACTUALLY COMPUTED OVER</b> — not the number
/// of things that ended. MEASURED 2026-09-04: it used to be <c>closed.Count</c> while the average ran over the
/// negative-span-filtered list, so a report with one corrupt row said "over 16" beside a mean of 15. An average
/// whose stated denominator is not its real one cannot be checked by the person reading it.</para>
/// </summary>
public sealed record WorkReportDuration(double? AverageDays, double? MedianDays, int Count);

/// <summary>
/// HOW MUCH WORK IS STILL OPEN, and how long it has been waiting — measured AT THE END OF THE PERIOD.
///
/// <para><b>⚠ AGAINST THE PERIOD'S END, NEVER AGAINST "NOW", AND THAT IS WHAT MAKES IT EVIDENCE.</b> A report
/// is read again months later — in a review, in an audit, beside a decision somebody already took. Ageing
/// computed from the current clock gives a different answer every time the page is opened, so the same period
/// stops reconciling with the copy that was printed. Anchored to <c>To</c>, June's report says the same thing in
/// June and in December.</para>
///
/// <para><b>Work with no deadline is counted here.</b> It is precisely the work
/// <see cref="WorkReportTimeliness"/> cannot speak about — <c>WithoutDueDate</c> is its blind spot — so ageing
/// is the measure that sees it. Every open task has an age; not every one has a promise.</para>
/// </summary>
/// <param name="UpTo7Days">Open at period end, created within the last week of it.</param>
/// <param name="From8To30Days">Open at period end, between eight and thirty days old.</param>
/// <param name="OlderThan30Days">Open at period end and older than thirty days.</param>
public sealed record WorkReportAging(int UpTo7Days, int From8To30Days, int OlderThan30Days)
{
    /// <summary>Every open item, once. The three buckets partition the same set, so this is their sum.</summary>
    public int Total => UpTo7Days + From8To30Days + OlderThan30Days;
}

/// <summary>
/// DID IT LAND ON TIME — over closed tasks that HAD a deadline.
///
/// <para><c>WithoutDueDate</c> is reported rather than folded into "on time": work nobody set a date for was not
/// early, and counting it as on-time is how a punctuality figure becomes flattering and useless.</para>
/// </summary>
public sealed record WorkReportTimeliness(int OnTime, int Late, int WithoutDueDate);

/// <summary>
/// DID THE PLAN HOLD — estimate against actual, over tasks that carry BOTH.
///
/// <para>⚠ Reported as hours and a task count, never as a ratio or a score. The variance is a fact about the
/// PLAN, and it is grouped by type or unit for that reason. See this file's header for why a per-person
/// percentage is excluded outright.</para>
/// </summary>
public sealed record WorkReportEffort(decimal EstimatedHours, decimal SpentHours, int TaskCount);

/// <summary>How the work ended, by the code the type's closure dictionary supplied (Faz 3).</summary>
public sealed record WorkReportOutcomeCount(string Code, int Count);

/// <summary>
/// HOW MUCH WORK CAME BACK — the raw material of the rework rate, as counts (Faz 4).
///
/// <para>Both numbers, because they answer different questions: five returns spread over five tasks is a team
/// under pressure, and five returns on one task is a task nobody can finish. A single number hides which.</para>
///
/// <para>⚠ A COUNT, never a rate. A rate needs a denominator and a period the reader agrees with, and inventing
/// one here would put a second answer beside whatever the screen (5b) decides to divide by.</para>
/// </summary>
public sealed record WorkReportRework(int TasksReturned, int TotalReturns);

/// <summary>
/// The whole answer: the period asked for, the totals, and the groups when one was requested.
/// </summary>
/// <param name="ScopeApplied">
/// What the numbers cover — <c>tenant</c> or <c>scoped</c>. Stated rather than implied so a reader of the
/// screen, or of a support ticket, can tell "there is no work" from "there is no work I may see".
/// </param>
/// <param name="GroupsTruncated">
/// How many groups were folded into <see cref="WorkReportDto.OtherKey"/> because the answer exceeded
/// <see cref="WorkReportDto.MaxGroups"/>. Zero when everything fits.
///
/// <para><b>⚠ STATED, NEVER SILENT.</b> Before this slice the grouping had no cap and no order at all: a
/// 500-person tenant grouped by assignee returned 500 buckets in whatever order the dictionary produced. A cap
/// alone would have been worse than none — a reader comparing units would be comparing the arbitrary fifty that
/// survived. The count travels so the screen can say "50 shown, N more", and the folded rows still appear in
/// the "other" bucket so the parts continue to add up to the whole.</para>
/// </param>
/// <param name="Previous">
/// The SAME NUMBERS for the period immediately before this one, of the same length — or null when the caller
/// asked not to compare.
///
/// <para><b>⚠ THE SERVER DECIDES WHAT "THE PREVIOUS PERIOD" IS, and the screen never computes it.</b> Two
/// places answering "which days came before these" drift apart by a day the first time somebody reasons about
/// month lengths, inclusive ends or a leap year — and then two charts on the same page disagree with no way to
/// tell which is right. One definition, computed once: <c>[From − length, From)</c>, which is exactly as long
/// as the period asked for and touches none of its days.</para>
///
/// <para>Carried as a whole <see cref="WorkReportBucket"/> rather than a handful of deltas, so the screen can
/// compare whichever measures it shows without the contract having to guess in advance which those are — and
/// so a DIRECTION is always derivable from two real numbers rather than from a pre-computed arrow nobody can
/// check.</para>
/// </param>
/// <summary>
/// The period immediately before the one asked about, and its totals.
///
/// <para>The bounds travel with the numbers so a reader — or a support ticket — can see WHICH days were
/// compared. A comparison whose other half is unnamed is a number nobody can reproduce.</para>
/// </summary>
public sealed record WorkReportComparison(DateTimeOffset From, DateTimeOffset To, WorkReportBucket Totals);

public sealed record WorkReportDto(
    DateTimeOffset From,
    DateTimeOffset To,
    string ScopeApplied,
    WorkReportGroupBy GroupBy,
    WorkReportBucket Totals,
    IReadOnlyList<WorkReportBucket> Groups,
    int GroupsTruncated = 0,
    WorkReportComparison? Previous = null)
{
    public const string ScopeTenant = "tenant";
    public const string ScopeScoped = "scoped";

    /// <summary>
    /// Groups beyond the cap. FIFTY is a reading limit, not a technical one: a bar chart of more than fifty rows
    /// is a wall nobody reads, and the tail is where the counts are smallest and the noise is highest.
    /// </summary>
    public const int MaxGroups = 50;

    /// <summary>
    /// The bucket everything past the cap is folded into. A reserved key rather than a translated word, because
    /// it crosses the wire and the screen names it in the reader's language.
    /// </summary>
    public const string OtherKey = "__other__";

    /// <summary>
    /// Work whose company could not be determined — its organisation unit is missing from the tenant's live
    /// set (deleted, or an id pointing at nothing).
    ///
    /// <para>⚠ MEASURED 2026-09-04, and it corrects an assumption worth stating: <c>TaskItem.OrganizationUnitId</c>
    /// is <c>required Guid</c>, NOT nullable, and <c>OrganizationUnit.LegalEntityId</c> is required too. So a
    /// task never simply "has no unit" — it has a unit id that may no longer RESOLVE. That is the case this
    /// bucket holds, and it is why the bucket cannot be dropped: a silently discarded row makes the groups fail
    /// to add up to the totals, with nothing on screen to explain the difference.</para>
    /// </summary>
    public const string UnassignedKey = "__unassigned__";

    /// <summary>
    /// The fail-closed answer: the period the caller asked for, and nothing in it.
    ///
    /// <para>Shaped like a real report on purpose. A null, or a 403, would make "you may see nothing" look like
    /// "the report is broken" — and the screen would then need a second rendering path for a state that is not
    /// an error.</para>
    /// </summary>
    public static WorkReportDto Empty(DateTimeOffset from, DateTimeOffset to, WorkReportGroupBy groupBy) =>
        new(from, to, ScopeScoped, groupBy, EmptyBucket(null), []);

    public static WorkReportBucket EmptyBucket(string? key) => new(
        key,
        null,
        new WorkReportFlow(0, 0, 0, 0, 0),
        new WorkReportDuration(null, null, 0),
        new WorkReportDuration(null, null, 0),
        new WorkReportAging(0, 0, 0),
        new WorkReportTimeliness(0, 0, 0),
        new WorkReportEffort(0m, 0m, 0),
        [],
        new WorkReportRework(0, 0));
}

/// <summary>
/// The report's ONE read — declared here beside its criteria rather than in Domain, following the same
/// Application-port shape <c>IOutboxEventRepository</c> and <c>IRepositoryReadinessPort</c> use: the criteria
/// carry a <see cref="WorkReportScope"/>, which is an authorization concept, and Domain has no business knowing
/// about it.
///
/// <para><b>⚠ IT IS NOT <c>GetAllForTenantAsync</c> AND IT MUST NEVER BECOME IT.</b> MEASURED 2026-09-03: the
/// task repository had no period- or filter-scoped read at all — the only broad one was
/// <c>GetAllForTenantAsync</c>, which loads every task in the tenant. At today's volume "pull them all and
/// count in C#" and "aggregate in the database" both work; the difference is which one still works next year.
/// So the counting happens in the database, in one aggregation, and the period is required by the criteria.</para>
/// </summary>
public interface IWorkReportRepository
{
    /// <summary>
    /// The totals and, when an axis was asked for, the groups — computed by the database.
    ///
    /// <para>The rework figures come from the transition log rather than from the task, because that is where a
    /// return is recorded (<c>TaskTransitionKind.Returned</c>); the implementation joins the two by task id
    /// inside the same call so a caller never has to.</para>
    ///
    /// <para>An out-of-scope criteria set (<c>Scope.MatchesNothing</c>) must return EMPTY buckets. The handler
    /// short-circuits before calling, so this is the second lock on the same door.</para>
    /// </summary>
    Task<WorkReportDto> AggregateAsync(WorkReportCriteria criteria, CancellationToken ct = default);
}
