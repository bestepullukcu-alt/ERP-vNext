using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;
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
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly ITaskTypeRepository _taskTypes;
    private readonly ILegalEntityReferenceValidator _legalEntities;
    private readonly ILogger<WorkReportRepository> _logger;

    public WorkReportRepository(
        IPlatformDbContext dbContext,
        ITenantContext tenantContext,
        IOrganizationUnitRepository organizationUnits,
        ITaskTypeRepository taskTypes,
        ILegalEntityReferenceValidator legalEntities,
        ILogger<WorkReportRepository> logger)
    {
        _tasks = dbContext.Database.GetCollection<TaskItem>(PlatformCollections.TaskItems);
        _transitions = dbContext.Database.GetCollection<TaskTransition>(PlatformCollections.TaskTransitions);
        _tenantContext = tenantContext;
        _organizationUnits = organizationUnits;
        _taskTypes = taskTypes;
        _legalEntities = legalEntities;
        _logger = logger;
    }

    public async Task<WorkReportDto> AggregateAsync(WorkReportCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var report = await MeasureAsync(criteria, ct);
        if (!criteria.ComparePrevious)
        {
            return report;
        }

        /*
         * ⚠ THE PREVIOUS PERIOD IS DEFINED ONCE, HERE, AND THE SCREEN NEVER COMPUTES IT.
         *
         * Two places answering "which days came before these" drift apart the first time somebody reasons
         * about month lengths, inclusive ends or a leap year — and then two figures on the same page disagree
         * with no way to tell which is right. The definition: the SAME LENGTH, immediately before, touching
         * none of the same days — `[From − (To − From), From)`. Half-open at both ends, like every interval in
         * this report.
         */
        var previousCriteria = PreviousPeriod(criteria);
        var previous = await MeasureAsync(previousCriteria, ct);

        return report with
        {
            Previous = new WorkReportComparison(previousCriteria.From, previousCriteria.To, previous.Totals)
        };
    }

    /// <summary>
    /// THE PERIOD IMMEDIATELY BEFORE THIS ONE — the single definition, named so a test can call it.
    ///
    /// <para><b>⚠ EXTRACTED FOR THE SAME REASON <see cref="BuildMatchFilter"/> WAS.</b> A test that worked the
    /// arithmetic out for itself would prove the TEST's idea of "previous", not this one — the exact gap that
    /// let a scope-dropping edit pass 47 green tests in Dilim 1a. Production calls this; so does the guard.</para>
    ///
    /// <para><b>The definition:</b> <c>[From − (To − From), From)</c> — the SAME LENGTH, immediately before,
    /// sharing no day. Half-open at both ends like every interval in this report, so the two halves are
    /// comparable and disjoint, which is the only thing that makes a direction drawn from them mean anything.
    /// A client computing this for itself would drift by a day the first time somebody reasoned about month
    /// lengths, and then two figures on one page would disagree with no way to tell which was right.</para>
    /// </summary>
    internal static WorkReportCriteria PreviousPeriod(WorkReportCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var length = criteria.To - criteria.From;

        return criteria with
        {
            From = criteria.From - length,
            To = criteria.From,
            // One level only: the comparison does not itself want a comparison, and asking for one would
            // recurse a period at a time until the epoch.
            ComparePrevious = false,
            // Groups are not compared in this slice — the totals are what a direction is drawn from, and
            // measuring fifty groups twice to show one arrow is work nobody asked for.
            GroupBy = WorkReportGroupBy.None
        };
    }

    private async Task<WorkReportDto> MeasureAsync(WorkReportCriteria criteria, CancellationToken ct)
    {

        /*
         * ⚠ THE SECOND LOCK ON THE SAME DOOR. The handler short-circuits an empty scope before it ever gets
         * here — and this checks again anyway, because "no scope" reaching a query builder is one careless
         * `if` away from "no filter", and an unfiltered report is wrong in a way that renders perfectly.
         */
        if (criteria.Scope.MatchesNothing)
        {
            return WorkReportDto.Empty(criteria.From, criteria.To, criteria.GroupBy);
        }

        var scoped = ScopeFilter(_tenantContext.TenantId, criteria.Scope);
        var inPeriod = BuildMatchFilter(_tenantContext.TenantId, criteria);

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
                task.Lifecycle,
                /*
                 * ⚠ BOTH PASSED EXPLICITLY even though the record defaults them: an EXPRESSION TREE cannot call
                 * a constructor with optional arguments (CS0854), and the projection is one. They are filled in
                 * the join below, from data the task does not carry.
                 */
                null,
                null))
            .ToListAsync(ct);

        /*
         * THE COMPANY, JOINED THROUGH THE UNIT — the one derived dimension in this report.
         *
         * MEASURED 2026-09-04: `TaskItem` carries no legal-entity field. It carries `OrganizationUnitId`
         * (`required Guid`), and `OrganizationUnit.LegalEntityId` is required in turn. So the company is a
         * lookup, and the org tree is read ONCE for the whole page — the same batched shape every other read
         * here uses. Per-row resolution would be an N+1 against the units collection.
         */
        var units = (await _organizationUnits.GetAllAsync(ct))
            .Where(unit => unit.TenantId == _tenantContext.TenantId && unit.DeletedAt is null)
            .ToDictionary(unit => unit.Id);

        var types = criteria.Filter?.TaskTypeCode is not null || criteria.GroupBy == WorkReportGroupBy.TaskType
            ? (await _taskTypes.ListAllAsync(ct)).ToDictionary(type => type.Id)
            : new Dictionary<Guid, Domain.Entities.Tasks.TaskType>();

        rows = rows
            .Select(row => row with
            {
                // Null when the unit is gone from the live set — the row then lands in the "unassigned"
                // bucket rather than being dropped. See WorkReportDto.UnassignedKey.
                LegalEntityId = units.TryGetValue(row.OrganizationUnitId, out var unit) ? unit.LegalEntityId : null,
                TaskTypeCode = row.TaskTypeId is { } typeId && types.TryGetValue(typeId, out var type)
                    ? type.Code
                    : null
            })
            /*
             * ⚠ THE DERIVED HALF OF THE FILTER, APPLIED HERE — and still an intersection.
             *
             * Company and type-code are not columns on a task, so Mongo cannot match them; they are resolved
             * above and filtered now. The rows this sees have ALREADY passed the scope and the direct filter in
             * the database, so this can only remove more — never add one back.
             */
            .Where(row => WorkReportTally.MatchesFilter(criteria.Filter, row))
            .ToList();

        /*
         * ⚠ UNATTENDED IS ITS OWN MATCH, and deliberately not part of the period.
         *
         * Oracle's Unattended report asks "how much work is sitting unclaimed" — a question about NOW, not about
         * a window. A task opened last year and never picked up is exactly what it is for, and folding it into
         * the period filter would hide precisely the rows that matter most.
         */
        var unattended = await CountUnattendedAsync(scoped, criteria, units, types, ct);

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
        return WorkReportTally.Build(
            criteria,
            rows,
            unattended,
            returnsByTask,
            await LabelsAsync(criteria, rows, units, types, ct),
            await OpenAtPeriodEndAsync(scoped, criteria, units, types, ct));
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
    internal static FilterDefinition<TaskItem> ScopeFilter(Guid tenantId, WorkReportScope scope)
    {
        var tenant = Builders<TaskItem>.Filter.And(
            Builders<TaskItem>.Filter.Eq(x => x.TenantId, tenantId),
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


    /// <summary>
    /// THE QUERY THIS REPORT ACTUALLY RUNS — scope AND period AND filter, composed once.
    ///
    /// <para><b>⚠ EXTRACTED SO A TEST CAN WATCH THE REAL COMPOSITION, and that is not cosmetic.</b> The
    /// intersection rule (a filter narrows the caller's scope and can never replace it) was covered only by a
    /// test that rebuilt the order itself, in the test — so it proved the TEST's composition, not this one.
    /// CONTROL TOWER demonstrated the gap: swapping <c>scoped</c> for <c>Filter.Empty</c> whenever a filter was
    /// present — "they asked for one unit, so query that unit directly" — left the whole suite green while the
    /// scope had silently stopped applying. That is a privilege-escalation seam that renders perfectly.</para>
    ///
    /// <para>Now there is ONE named composition. Production calls it; <c>WorkReportQueryCompositionTests</c>
    /// renders it to BSON and asserts the tenant and scope clauses survive alongside every filter. A change
    /// that drops the scope changes the rendered query, and the guard sees it.</para>
    ///
    /// <para>The ORDER of the three terms is not what makes this safe — <c>$and</c> is commutative. What makes
    /// it safe is that the scope is a TERM AT ALL: there is no arrangement of ANDed clauses that adds a row
    /// back, so the only way to widen is to remove the scope, which is exactly what the guard watches for.</para>
    /// </summary>
    internal static FilterDefinition<TaskItem> BuildMatchFilter(Guid tenantId, WorkReportCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return Builders<TaskItem>.Filter.And(
            // WHOSE rows exist at all. Always present, whatever else was asked for.
            ScopeFilter(tenantId, criteria.Scope),
            /*
             * TOUCHED IN THE PERIOD — opened in it, or closed in it. A task opened in March and closed in June
             * belongs to both months' reports for different reasons, and filtering on `CreatedAt` alone would
             * drop it from June's closure figures entirely.
             */
            Builders<TaskItem>.Filter.Or(
                Between(x => x.CreatedAt, criteria.From, criteria.To),
                Between(x => x.CompletedAt, criteria.From, criteria.To),
                Between(x => x.CancelledAt, criteria.From, criteria.To)),
            // ⚠ ANDed ONTO THE SCOPE, NEVER INSTEAD OF IT.
            DirectFilter(criteria.Filter));
    }

    /// <summary>
    /// The half of the reader's filter a TASK can answer on its own — matched in the database, so the rows that
    /// travel are already narrowed.
    ///
    /// <para>Company and type-code are absent on purpose: neither is a field on a task
    /// (<c>OrganizationUnit.LegalEntityId</c> and <c>TaskType.Code</c> live elsewhere), so they are resolved and
    /// applied after the read. An empty filter contributes <c>Filter.Empty</c>, which is Mongo's identity — the
    /// unfiltered query is byte-for-byte the one that ran before filters existed.</para>
    /// </summary>
    private static FilterDefinition<TaskItem> DirectFilter(WorkReportFilter? filter)
    {
        if (filter is null || filter.IsEmpty)
        {
            return Builders<TaskItem>.Filter.Empty;
        }

        var clauses = new List<FilterDefinition<TaskItem>>();

        if (filter.OrganizationUnitId is { } unit)
        {
            clauses.Add(Builders<TaskItem>.Filter.Eq(x => x.OrganizationUnitId, unit));
        }

        if (filter.AssigneeUserId is { } assignee)
        {
            clauses.Add(Builders<TaskItem>.Filter.Eq(x => x.AssigneeUserId, (Guid?)assignee));
        }

        if (filter.Priority is { } priority)
        {
            clauses.Add(Builders<TaskItem>.Filter.Eq(x => x.Priority, priority));
        }

        return clauses.Count == 0 ? Builders<TaskItem>.Filter.Empty : Builders<TaskItem>.Filter.And(clauses);
    }


    /// <summary>
    /// The work that was STILL OPEN at the end of the period — ageing's own row set.
    ///
    /// <para><b>⚠ A SEPARATE READ, AND IT HAS TO BE.</b> The report's main query matches work TOUCHED in the
    /// period — created or closed inside it. A task raised last year and still untouched matches NEITHER
    /// clause, and it is precisely the task ageing exists to surface. Deriving ageing from the period's rows
    /// would show a clean backlog on the tenant with the worst one.</para>
    ///
    /// <para>Bounded by the same scope and the same filter as everything else, and projected to the four fields
    /// ageing and grouping need — never the whole task.</para>
    /// </summary>
    private async Task<IReadOnlyList<WorkReportRow>> OpenAtPeriodEndAsync(
        FilterDefinition<TaskItem> scoped,
        WorkReportCriteria criteria,
        IReadOnlyDictionary<Guid, OrganizationUnit> units,
        IReadOnlyDictionary<Guid, Domain.Entities.Tasks.TaskType> types,
        CancellationToken ct)
    {
        /*
         * OPEN AT AN INSTANT: created before it, and not closed by then. Expressed against the stored
         * timestamps rather than the lifecycle enum, because a task closed AFTER the period was open DURING it
         * — the enum only knows about today.
         */
        var openThen = Builders<TaskItem>.Filter.And(
            scoped,
            DirectFilter(criteria.Filter),
            Builders<TaskItem>.Filter.Lt(x => x.CreatedAt, criteria.To),
            Builders<TaskItem>.Filter.Or(
                Builders<TaskItem>.Filter.Eq(x => x.CompletedAt, (DateTimeOffset?)null),
                Builders<TaskItem>.Filter.Gte(x => x.CompletedAt, criteria.To)),
            Builders<TaskItem>.Filter.Or(
                Builders<TaskItem>.Filter.Eq(x => x.CancelledAt, (DateTimeOffset?)null),
                Builders<TaskItem>.Filter.Gte(x => x.CancelledAt, criteria.To)));

        var rows = await _tasks.Aggregate()
            .Match(openThen)
            .Project(task => new
            {
                task.Id,
                task.CreatedAt,
                task.OrganizationUnitId,
                task.TaskTypeId,
                task.AssigneeUserId,
                task.Priority
            })
            .ToListAsync(ct);

        return rows
            .Select(row => new WorkReportRow(
                row.Id,
                row.TaskTypeId,
                row.OrganizationUnitId,
                row.AssigneeUserId,
                CreatedByUserId: null,
                PoolPositionId: null,
                row.Priority,
                row.CreatedAt,
                CompletedAt: null,
                CancelledAt: null,
                DueAt: null,
                EstimateHours: null,
                SpentHours: 0m,
                ClosureReasonCode: null,
                Lifecycle: TaskLifecycle.Open,
                LegalEntityId: units.TryGetValue(row.OrganizationUnitId, out var unit) ? unit.LegalEntityId : null,
                TaskTypeCode: row.TaskTypeId is { } typeId && types.TryGetValue(typeId, out var type) ? type.Code : null))
            // The derived half of the filter, same as the main read: company and type code are not task columns.
            .Where(row => WorkReportTally.MatchesFilter(criteria.Filter, row))
            .ToList();
    }

    /// <summary>
    /// Open work nobody is holding — counted as of NOW, and narrowed by the SAME filter as everything else.
    ///
    /// <para>⚠ The filter has to reach this number too. A reader who asked about one unit and saw the whole
    /// tenant's unclaimed backlog beside that unit's flow would draw a conclusion about the unit from a figure
    /// that was never about it.</para>
    ///
    /// <para>Counted in the database when the filter is direct-only, and read back when a derived clause
    /// (company, type code) applies — the second path is bounded by the same scope, and the alternative is a
    /// number that quietly ignores half the filter.</para>
    /// </summary>
    private async Task<int> CountUnattendedAsync(
        FilterDefinition<TaskItem> scoped,
        WorkReportCriteria criteria,
        IReadOnlyDictionary<Guid, OrganizationUnit> units,
        IReadOnlyDictionary<Guid, Domain.Entities.Tasks.TaskType> types,
        CancellationToken ct)
    {
        var open = Builders<TaskItem>.Filter.And(
            scoped,
            DirectFilter(criteria.Filter),
            Builders<TaskItem>.Filter.Eq(x => x.AssigneeUserId, (Guid?)null),
            Builders<TaskItem>.Filter.Nin(x => x.Lifecycle, new[] { TaskLifecycle.Done, TaskLifecycle.Cancelled }));

        var derived = criteria.Filter is { } f && (f.LegalEntityId is not null || !string.IsNullOrWhiteSpace(f.TaskTypeCode));
        if (!derived)
        {
            return (int)await _tasks.CountDocumentsAsync(open, cancellationToken: ct);
        }

        var candidates = await _tasks.Aggregate()
            .Match(open)
            .Project(task => new { task.OrganizationUnitId, task.TaskTypeId })
            .ToListAsync(ct);

        return candidates.Count(candidate =>
            (criteria.Filter!.LegalEntityId is not { } company
                || (units.TryGetValue(candidate.OrganizationUnitId, out var unit) && unit.LegalEntityId == company))
            && (string.IsNullOrWhiteSpace(criteria.Filter.TaskTypeCode)
                || (candidate.TaskTypeId is { } typeId
                    && types.TryGetValue(typeId, out var type)
                    && string.Equals(type.Code, criteria.Filter.TaskTypeCode.Trim(), StringComparison.OrdinalIgnoreCase))));
    }

    /// <summary>
    /// The WORDS for each group key, from the data Platform owns.
    ///
    /// <para><b>⚠ THE ASSIGNEE AXIS IS DELIBERATELY ABSENT.</b> MEASURED 2026-09-04: Platform has no User
    /// entity and no auth client, so it cannot name a person. Returning an id-shaped "label" would be a
    /// fabrication; the screen resolves people through the user lookup it already uses, and the bucket's
    /// <c>Label</c> stays null so the screen knows to.</para>
    ///
    /// <para>Resolved only for the groups that SURVIVED the cap and only for the axis in play — a company name
    /// is a cross-service call to MDM, and fifty of them for buckets nobody will see is fifty round-trips spent
    /// on nothing.</para>
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> LabelsAsync(
        WorkReportCriteria criteria,
        IReadOnlyList<WorkReportRow> rows,
        IReadOnlyDictionary<Guid, OrganizationUnit> units,
        IReadOnlyDictionary<Guid, Domain.Entities.Tasks.TaskType> types,
        CancellationToken ct)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);

        switch (criteria.GroupBy)
        {
            case WorkReportGroupBy.OrganizationUnit:
                foreach (var id in rows.Select(row => row.OrganizationUnitId).Distinct())
                {
                    // Absent when the unit is gone. Null label, identity on screen — never an invented name.
                    if (units.TryGetValue(id, out var unit))
                    {
                        labels[id.ToString()] = unit.Name;
                    }
                }

                break;

            case WorkReportGroupBy.TaskType:
                var allTypes = types.Count > 0 ? types : (await _taskTypes.ListAllAsync(ct)).ToDictionary(t => t.Id);
                foreach (var id in rows.Select(row => row.TaskTypeId).Where(id => id is not null).Select(id => id!.Value).Distinct())
                {
                    if (allTypes.TryGetValue(id, out var type))
                    {
                        labels[id.ToString()] = type.Name;
                    }
                }

                break;

            case WorkReportGroupBy.LegalEntity:
                await AddLegalEntityLabelsAsync(rows, labels, ct);
                break;

            /*
             * Priority is an ENUM and Assignee is a person. The first is named by the SCREEN in the reader's
             * language — a server-side English word would be a second, untranslated vocabulary. The second
             * cannot be named here at all.
             */
            default:
                break;
        }

        return labels;
    }

    private async Task AddLegalEntityLabelsAsync(
        IReadOnlyList<WorkReportRow> rows,
        Dictionary<string, string> labels,
        CancellationToken ct)
    {
        var companies = rows
            .Select(row => row.LegalEntityId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .Take(WorkReportDto.MaxGroups)
            .ToList();

        foreach (var id in companies)
        {
            try
            {
                /*
                 * ⚠ A CROSS-SERVICE CALL. LegalEntity lives in MdmService; Platform reaches it only through this
                 * validator, one id at a time. Bounded by the group cap above, and by the fact that a group has
                 * a handful of companies rather than hundreds.
                 */
                var response = await _legalEntities.ValidateAsync(id, ct);
                if (response.IsSuccessful && response.Data is { } company)
                {
                    labels[id.ToString()] = string.IsNullOrWhiteSpace(company.DisplayName)
                        ? company.LegalName
                        : company.DisplayName!;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                /*
                 * A NAME IS NOT WORTH FAILING A REPORT FOR. MDM being unreachable leaves this company's label
                 * null and the screen shows its identity — the numbers are Platform's and are still correct.
                 * Throwing would take a whole report down over a display string.
                 */
                _logger.LogWarning(ex, "Legal entity {LegalEntityId} could not be named for the work report.", id);
            }
        }
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
