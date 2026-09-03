using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// MOD-0024 Faz 5a — the work report, COUNTED IN THE DATABASE.
///
/// <para><b>⚠ WHY NOT "LOAD THEM ALL AND COUNT IN C#".</b> MEASURED 2026-09-03: <c>ITaskItemRepository</c> had no
/// period- or filter-scoped read at all; the only broad one is <c>GetAllForTenantAsync</c>, which pulls every
/// task in the tenant. At today's volume both approaches answer correctly and quickly. The difference is which
/// one still answers a year from now — a report is the one screen whose cost grows with everything the tenant
/// has ever done, so it is the last place to write a full scan.</para>
///
/// <para><b>The shape.</b> <c>Aggregate().Match(...).Group(...)</c>, the same pattern
/// <c>ModuleCatalogRepository.GetStatsAsync</c> and <c>TenantRegistryRepository</c> already use here. The match
/// is built ONCE from the criteria and reused for every measure, so the scope cannot be applied to one number
/// and forgotten on the next.</para>
///
/// <para><b>Two collections, because a return lives in the log.</b> Task rows answer flow, cycle time,
/// timeliness, effort and outcomes; <c>TaskTransitionKind.Returned</c> rows answer rework. They are joined by
/// task id inside this call so no caller has to know that.</para>
/// </summary>
public sealed class WorkReportRepository : IWorkReportRepository
{
    private readonly IMongoCollection<TaskItem> _tasks;
    private readonly IMongoCollection<TaskTransition> _transitions;
    private readonly ITenantContext _tenantContext;

    public WorkReportRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
    {
        _tasks = dbContext.Database.GetCollection<TaskItem>(PlatformCollections.TaskItems);
        _transitions = dbContext.Database.GetCollection<TaskTransition>(PlatformCollections.TaskTransitions);
        _tenantContext = tenantContext;
    }

    public async Task<WorkReportDto> AggregateAsync(WorkReportCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        /*
         * ⚠ THE SECOND LOCK ON THE SAME DOOR. The handler short-circuits an empty scope before it ever gets
         * here — and this checks again anyway, because "no scope" reaching a query builder is one careless
         * `if` away from "no filter", and an unfiltered report is wrong in a way that renders perfectly.
         */
        if (criteria.Scope.MatchesNothing)
        {
            return WorkReportDto.Empty(criteria.From, criteria.To, criteria.GroupBy);
        }

        var scoped = ScopeFilter(criteria.Scope);

        /*
         * TOUCHED IN THE PERIOD — opened in it, or closed in it. A task opened in March and closed in June
         * belongs to both months' reports for different reasons, and filtering on `CreatedAt` alone would drop
         * it from June's closure figures entirely.
         */
        var inPeriod = Builders<TaskItem>.Filter.And(
            scoped,
            Builders<TaskItem>.Filter.Or(
                Between(x => x.CreatedAt, criteria.From, criteria.To),
                Between(x => x.CompletedAt, criteria.From, criteria.To),
                Between(x => x.CancelledAt, criteria.From, criteria.To)));

        var rows = await _tasks.Aggregate()
            .Match(inPeriod)
            /*
             * FIFTEEN FIELDS, not the whole task. A task carries its checklist, its field values and its frozen
             * document references; none of them can appear in a report, and shipping them would make the wire
             * cost of the report grow with data it never reads.
             */
            .Project(task => new WorkReportRow(
                task.Id,
                task.TaskTypeId,
                task.OrganizationUnitId,
                task.AssigneeUserId,
                task.CreatedByUserId,
                task.PoolPositionId,
                task.Priority,
                task.CreatedAt,
                task.CompletedAt,
                task.CancelledAt,
                task.DueAt,
                task.EstimateHours,
                task.SpentHours,
                task.ClosureReasonCode,
                task.Lifecycle))
            .ToListAsync(ct);

        /*
         * ⚠ UNATTENDED IS ITS OWN MATCH, and deliberately not part of the period.
         *
         * Oracle's Unattended report asks "how much work is sitting unclaimed" — a question about NOW, not about
         * a window. A task opened last year and never picked up is exactly what it is for, and folding it into
         * the period filter would hide precisely the rows that matter most.
         */
        var unattended = await _tasks.CountDocumentsAsync(
            Builders<TaskItem>.Filter.And(
                scoped,
                Builders<TaskItem>.Filter.Eq(x => x.AssigneeUserId, (Guid?)null),
                Builders<TaskItem>.Filter.Nin(x => x.Lifecycle, new[] { TaskLifecycle.Done, TaskLifecycle.Cancelled })),
            cancellationToken: ct);

        var returnsByTask = await ReturnsByTaskAsync(rows.Select(row => row.Id).ToList(), ct);

        /*
         * ⚠ THE SUMS LIVE IN `WorkReportTally`, NOT HERE — and that is what makes them testable.
         *
         * The expensive half is done: the match, the projection, the unattended count and the return histogram
         * all ran in the database, and `rows` is one period of one scope. The arithmetic is shared with the
         * tests, which check it against hand-computed expectations rather than against a live Mongo — this
         * module's Mongo-backed suites are the flaky ones, and a report whose sums are only verified when the
         * database happens to be up is a report whose sums are not verified.
         */
        return WorkReportTally.Build(criteria, rows, (int)unattended, returnsByTask);
    }

    /// <summary>
    /// How many times each of these tasks was RETURNED. Counted in the database and only for the tasks the
    /// report already matched, so the log is never scanned whole.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, int>> ReturnsByTaskAsync(
        IReadOnlyList<Guid> taskIds,
        CancellationToken ct)
    {
        if (taskIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var filter = Builders<TaskTransition>.Filter.And(
            Builders<TaskTransition>.Filter.Eq(x => x.TenantId, _tenantContext.TenantId),
            Builders<TaskTransition>.Filter.Eq(x => x.IsDeleted, false),
            Builders<TaskTransition>.Filter.Eq(x => x.Kind, TaskTransitionKind.Returned),
            Builders<TaskTransition>.Filter.In(x => x.TaskItemId, taskIds));

        var grouped = await _transitions.Aggregate()
            .Match(filter)
            .Group(x => x.TaskItemId, g => new { TaskItemId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return grouped.ToDictionary(x => x.TaskItemId, x => x.Count);
    }

    /// <summary>
    /// The scope, as a Mongo filter. Every branch is an OR — a caller who may see their unit AND their manager's
    /// pool sees both — and the tenant/soft-delete guard is ANDed over the whole thing regardless.
    /// </summary>
    private FilterDefinition<TaskItem> ScopeFilter(WorkReportScope scope)
    {
        var tenant = Builders<TaskItem>.Filter.And(
            Builders<TaskItem>.Filter.Eq(x => x.TenantId, _tenantContext.TenantId),
            Builders<TaskItem>.Filter.Eq(x => x.IsDeleted, false));

        if (scope.TenantWide)
        {
            return tenant;
        }

        var branches = new List<FilterDefinition<TaskItem>>();

        if (scope.OrganizationUnitIds.Count > 0)
        {
            branches.Add(Builders<TaskItem>.Filter.In(x => x.OrganizationUnitId, scope.OrganizationUnitIds));
        }

        if (scope.PositionIds.Count > 0)
        {
            branches.Add(Builders<TaskItem>.Filter.In(
                x => x.PoolPositionId, scope.PositionIds.Select(id => (Guid?)id)));
        }

        if (scope.UserIds.Count > 0)
        {
            var users = scope.UserIds.Select(id => (Guid?)id).ToList();
            branches.Add(Builders<TaskItem>.Filter.In(x => x.AssigneeUserId, users));
            branches.Add(Builders<TaskItem>.Filter.In(x => x.CreatedByUserId, users));
        }

        // Unreachable while MatchesNothing is checked above; kept because an OR of zero branches matches
        // EVERYTHING in Mongo, which is the one wrong answer this whole class exists to avoid.
        return branches.Count == 0
            ? Builders<TaskItem>.Filter.And(tenant, Builders<TaskItem>.Filter.Where(_ => false))
            : Builders<TaskItem>.Filter.And(tenant, Builders<TaskItem>.Filter.Or(branches));
    }

    private static FilterDefinition<TaskItem> Between(
        System.Linq.Expressions.Expression<Func<TaskItem, DateTimeOffset?>> field,
        DateTimeOffset from,
        DateTimeOffset to)
        => Builders<TaskItem>.Filter.And(
            Builders<TaskItem>.Filter.Gte(field, from),
            Builders<TaskItem>.Filter.Lt(field, to));

    private static FilterDefinition<TaskItem> Between(
        System.Linq.Expressions.Expression<Func<TaskItem, DateTimeOffset>> field,
        DateTimeOffset from,
        DateTimeOffset to)
        => Builders<TaskItem>.Filter.And(
            Builders<TaskItem>.Filter.Gte(field, from),
            Builders<TaskItem>.Filter.Lt(field, to));

}
