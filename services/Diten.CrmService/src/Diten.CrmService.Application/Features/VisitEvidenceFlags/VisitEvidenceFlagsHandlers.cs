using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.DoctorInsights;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitEvidenceFlags;

/// <summary>Whose starts the caller sees: everyone (<see cref="All"/>) or <see cref="Reps"/>; <see cref="Names"/> = rep → name.</summary>
public sealed record VisitEvidenceFlagScope(bool All, IReadOnlySet<string> Reps, IReadOnlyDictionary<string, string> Names)
{
    public bool Sees(string resourceId) => All || Reps.Contains(resourceId);

    public string NameOf(string resourceId) => Names.TryGetValue(resourceId, out var n) ? n : resourceId;
}

/// <summary>
/// WP-VW-W3E (BE-b, §2.6) — the manager's scope over the visit starts:
/// <list type="bullet">
/// <item><c>crm.visit-evidence-flags.read-all</c> ⇒ the tenant;</item>
/// <item><c>.read</c> ⇒ the caller + the reps with an ACTIVE PRIMARY assignment on the caller's TEAM nodes
/// (<see cref="TeamScopeResolver"/>: each subtree assignment's node + its active descendants) in a current model;</item>
/// <item>a rep / an execution outside the scope answers 404 exactly like a missing one (no leak).</item>
/// </list>
/// The rep names come from the same assignments (one pass over the current models — no per-row read).
/// </summary>
public sealed class VisitEvidenceFlagScopeResolver
{
    private const int ModelPageSize = 200;

    private readonly ICallerScope _caller;
    private readonly TeamScopeResolver _team;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryResourceAssignmentRepository _assignments;

    public VisitEvidenceFlagScopeResolver(ICallerScope caller, TeamScopeResolver team, ITerritoryModelRepository models,
        ITerritoryResourceAssignmentRepository assignments)
    {
        _caller = caller;
        _team = team;
        _models = models;
        _assignments = assignments;
    }

    public async Task<VisitEvidenceFlagScope> ResolveAsync(Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        var all = _caller.HasPermission(VisitEvidenceFlagsPermissions.ReadAll);
        var me = _caller.CallerResourceId;
        HashSet<Guid> teamNodes = new();
        if (!all && me is not null)
        {
            var current = await _team.CurrentAsync(tenantId, me, now, ct);
            if (current.Count > 0) teamNodes = await _team.TeamNodesAsync(tenantId, current, now, ct);
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var reps = new HashSet<string>(StringComparer.Ordinal);
        var (models, _) = await _models.ListAsync(tenantId, null, TerritoryCoverageLifecyclePolicy.CurrentModelStatus, 1, ModelPageSize, ct);
        foreach (var model in models.Where(m => TerritoryCoverageLifecyclePolicy.IsModelCurrent(m, now)))
        {
            foreach (var a in await _assignments.ListByModelAsync(tenantId, model.Id, ct))
            {
                if (!VisitEvidenceRepRoster.IsActivePrimary(a, now)) continue;
                var id = a.Resource.ResourceId;
                if (!names.ContainsKey(id)) names[id] = string.IsNullOrWhiteSpace(a.Resource.DisplayName) ? id : a.Resource.DisplayName.Trim();
                if (!all && a.TerritoryId is { } node && teamNodes.Contains(node)) reps.Add(id);
            }
        }

        if (!all && me is not null) reps.Add(me); // the caller's own starts
        return new VisitEvidenceFlagScope(all, reps, names);
    }
}

/// <summary>WP-VW-W3E (BE-b, §2.6) — the list, the summary and the detail. Reads only.</summary>
public sealed class VisitEvidenceFlagsQueryHandlers :
    IRequestHandler<GetVisitEvidenceFlagsQuery, Response<VisitEvidenceFlagPageDto>>,
    IRequestHandler<GetVisitEvidenceFlagsSummaryQuery, Response<VisitEvidenceFlagSummaryDto>>,
    IRequestHandler<GetVisitEvidenceFlagDetailQuery, Response<VisitEvidenceFlagDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly VisitEvidenceFlagScopeResolver _scope;
    private readonly IVisitExecutionRepository _executions;
    private readonly IPlannedVisitRepository _plans;
    private readonly IAccountRepository _accounts;
    private readonly IContactRepository _contacts;
    private readonly IVisitEvidenceExceptionRepository _exceptions;
    private readonly TimeProvider _clock;

    public VisitEvidenceFlagsQueryHandlers(ITenantContext tenant, VisitEvidenceFlagScopeResolver scope, IVisitExecutionRepository executions,
        IPlannedVisitRepository plans, IAccountRepository accounts, IContactRepository contacts, IVisitEvidenceExceptionRepository exceptions,
        TimeProvider? clock = null)
    {
        _tenant = tenant;
        _scope = scope;
        _executions = executions;
        _plans = plans;
        _accounts = accounts;
        _contacts = contacts;
        _exceptions = exceptions;
        _clock = clock ?? TimeProvider.System;
    }

    // ── the window ───────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>[from, to] in UTC days (the W1 day); default the last 30 days to today; at most 92 days, both included.</summary>
    public static (DateOnly From, DateOnly To)? Window(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to ?? today;
        var start = from ?? end.AddDays(-(VisitEvidenceFlagsLimits.DefaultRangeDays - 1));
        if (start > end || end.DayNumber - start.DayNumber + 1 > VisitEvidenceFlagsLimits.MaxRangeDays) return null;
        return (start, end);
    }

    private static DateTimeOffset StartOf(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static Response<T> RangeInvalid<T>()
        => Response<T>.Fail(new[] { $"from ≤ to and at most {VisitEvidenceFlagsLimits.MaxRangeDays} days.", VisitEvidenceFlagsErrors.RangeInvalid }, 400);

    private static Response<T> NotFound<T>()
        => Response<T>.Fail(new[] { "Not found.", VisitEvidenceFlagsErrors.NotFound }, 404);

    /// <summary>The reps the read covers: the scope, narrowed to one rep (outside the scope ⇒ null = 404).</summary>
    private static IReadOnlyCollection<string>? Reps(VisitEvidenceFlagScope scope, string? resourceId, out bool outside)
    {
        outside = false;
        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            var one = resourceId.Trim();
            if (!scope.Sees(one)) { outside = true; return null; }
            return new[] { one };
        }

        return scope.All ? null : scope.Reps.ToList();
    }

    // ── list ─────────────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<VisitEvidenceFlagPageDto>> Handle(GetVisitEvidenceFlagsQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Response<VisitEvidenceFlagPageDto>.Fail("Tenant context is required.", 400);
        var now = _clock.GetUtcNow();
        if (Window(request.From, request.To, DateOnly.FromDateTime(now.UtcDateTime)) is not { } window) return RangeInvalid<VisitEvidenceFlagPageDto>();

        var flags = (request.Flags ?? Array.Empty<string>()).Select(f => f.Trim()).Where(f => f.Length > 0).Distinct().ToList();
        if (flags.Any(f => !VisitExecutionFlags.All.Contains(f, StringComparer.Ordinal)))
        {
            return Response<VisitEvidenceFlagPageDto>.Fail(new[] { $"flags are any of: {string.Join(", ", VisitExecutionFlags.All)}.", VisitEvidenceFlagsErrors.FilterInvalid }, 400);
        }

        var scope = await _scope.ResolveAsync(tenantId, now, ct);
        var reps = Reps(scope, request.ResourceId, out var outside);
        if (outside) return NotFound<VisitEvidenceFlagPageDto>();

        IReadOnlyCollection<Guid>? planIds = null;
        if (request.AccountId is { } accountId)
        {
            planIds = await _plans.ListIdsByAccountAndDateRangeAsync(tenantId, accountId, window.From, window.To, ct);
        }

        var page = Math.Max(1, request.Page ?? 1);
        var size = Math.Clamp(request.PageSize ?? VisitEvidenceFlagsLimits.DefaultPageSize, 1, VisitEvidenceFlagsLimits.MaxPageSize);
        var filter = new VisitEvidenceFlagFilter(StartOf(window.From), StartOf(window.To.AddDays(1)), reps, flags, request.OnlyFlagged, planIds);
        var (items, total) = planIds is { Count: 0 }
            ? (Array.Empty<Domain.Entities.VisitExecution>(), 0L)
            : await _executions.ListForEvidenceFlagsAsync(tenantId, filter, (page - 1) * size, size, ct);

        var rows = await RowsAsync(tenantId, items, scope, ct);
        return Response<VisitEvidenceFlagPageDto>.Success(new VisitEvidenceFlagPageDto(rows, total, page, size, window.From, window.To));
    }

    /// <summary>The rows of one page: the plans, institutions and doctors in ONE read each (no per-row read).</summary>
    private async Task<IReadOnlyList<VisitEvidenceFlagRowDto>> RowsAsync(
        Guid tenantId, IReadOnlyList<Domain.Entities.VisitExecution> items, VisitEvidenceFlagScope scope, CancellationToken ct)
    {
        if (items.Count == 0) return Array.Empty<VisitEvidenceFlagRowDto>();
        var plans = (await _plans.ListByIdsAsync(tenantId, items.Select(e => e.PlannedVisitId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);
        var accountIds = plans.Values.Select(p => p.AccountId ?? (IsAccountTarget(p) ? p.TargetId : (Guid?)null))
            .Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var contactIds = plans.Values.Select(p => p.ContactId).Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var accounts = accountIds.Count == 0 ? new Dictionary<Guid, string>()
            : (await _accounts.ListByIdsAsync(tenantId, accountIds, ct)).ToDictionary(a => a.Id, a => a.AccountName);
        var contacts = contactIds.Count == 0 ? new Dictionary<Guid, string>()
            : (await _contacts.ListByIdsAsync(tenantId, contactIds, ct)).ToDictionary(c => c.Id, c => c.DisplayName);

        return items.Select(e => Row(e, plans.TryGetValue(e.PlannedVisitId, out var p) ? p : null, accounts, contacts, scope)).ToList();
    }

    private static bool IsAccountTarget(Domain.Entities.PlannedVisit p)
        => string.Equals(p.TargetType, PlannedVisitTargetType.Account, StringComparison.OrdinalIgnoreCase)
           || string.Equals(p.TargetType, PlannedVisitTargetType.Pharmacy, StringComparison.OrdinalIgnoreCase);

    private static VisitEvidenceFlagRowDto Row(Domain.Entities.VisitExecution e, Domain.Entities.PlannedVisit? plan,
        IReadOnlyDictionary<Guid, string> accounts, IReadOnlyDictionary<Guid, string> contacts, VisitEvidenceFlagScope scope)
    {
        var accountId = plan is null ? null : plan.AccountId ?? (IsAccountTarget(plan) ? plan.TargetId : (Guid?)null);
        var accountName = accountId is { } a && accounts.TryGetValue(a, out var an) ? an : null;
        var targetName = plan?.ContactId is { } c && contacts.TryGetValue(c, out var cn) ? cn : accountName;
        return new VisitEvidenceFlagRowDto(
            e.Id, e.PlannedVisitId, e.StartedAt, e.ResourceId, scope.NameOf(e.ResourceId),
            new VisitEvidenceTargetDto(plan?.TargetType ?? string.Empty, plan?.TargetId ?? Guid.Empty, targetName, accountId, accountName),
            e.Kind, e.Flags.ToList(),
            new VisitEvidenceLocationSummaryDto(e.Location.DistanceMeters, e.Location.AccuracyMeters, e.Location.RadiusMeters, e.Location.Mode, e.Location.Status),
            DeviceEvidenceRules.ToDto(e.Device), e.DurationSeconds, e.ExceptionId, e.Photo is not null);
    }

    // ── summary ──────────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<VisitEvidenceFlagSummaryDto>> Handle(GetVisitEvidenceFlagsSummaryQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Response<VisitEvidenceFlagSummaryDto>.Fail("Tenant context is required.", 400);
        var now = _clock.GetUtcNow();
        if (Window(request.From, request.To, DateOnly.FromDateTime(now.UtcDateTime)) is not { } window) return RangeInvalid<VisitEvidenceFlagSummaryDto>();

        var scope = await _scope.ResolveAsync(tenantId, now, ct);
        var reps = Reps(scope, request.ResourceId, out var outside);
        if (outside) return NotFound<VisitEvidenceFlagSummaryDto>();

        var rows = await _executions.ListForEvidenceSummaryAsync(tenantId,
            new VisitEvidenceFlagFilter(StartOf(window.From), StartOf(window.To.AddDays(1)), reps, null, false, null), ct);
        return Response<VisitEvidenceFlagSummaryDto>.Success(Summarize(rows, scope, window.From, window.To));
    }

    /// <summary>The counts the list would give: started = every start, flagged = a start with at least one flag (the list's
    /// <c>onlyFlagged</c>), the rate guarded against 0 starts, per flag and per rep (the most frequent flag; ties by code).</summary>
    public static VisitEvidenceFlagSummaryDto Summarize(IReadOnlyList<Domain.Entities.VisitExecution> rows, VisitEvidenceFlagScope scope, DateOnly from, DateOnly to)
    {
        var started = rows.Count;
        var flagged = rows.Count(r => r.Flags.Count > 0);
        var byFlag = rows.SelectMany(r => r.Flags.Distinct()).GroupBy(f => f, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var perRep = rows.GroupBy(r => r.ResourceId, StringComparer.Ordinal).Select(g => new VisitEvidenceRepSummaryDto(
                g.Key, scope.NameOf(g.Key), g.Count(), g.Count(r => r.Flags.Count > 0),
                g.SelectMany(r => r.Flags.Distinct()).GroupBy(f => f, StringComparer.Ordinal)
                    .OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key).FirstOrDefault()))
            .OrderByDescending(r => r.Flagged).ThenBy(r => r.RepName, StringComparer.CurrentCultureIgnoreCase).ToList();
        return new VisitEvidenceFlagSummaryDto(from, to, started, flagged, started == 0 ? 0d : Math.Round((double)flagged / started, 4), byFlag, perRep);
    }

    // ── detail ───────────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<VisitEvidenceFlagDetailDto>> Handle(GetVisitEvidenceFlagDetailQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Response<VisitEvidenceFlagDetailDto>.Fail("Tenant context is required.", 400);
        var execution = request.ExecutionId == Guid.Empty ? null : await _executions.GetByIdAsync(tenantId, request.ExecutionId, ct);
        if (execution is null) return NotFound<VisitEvidenceFlagDetailDto>();
        var scope = await _scope.ResolveAsync(tenantId, _clock.GetUtcNow(), ct);
        if (!scope.Sees(execution.ResourceId)) return NotFound<VisitEvidenceFlagDetailDto>(); // outside = missing

        var row = (await RowsAsync(tenantId, new[] { execution }, scope, ct))[0];
        VisitEvidenceExceptionSummaryDto? exception = null;
        if (execution.ExceptionId is { } exceptionId && await _exceptions.GetByIdAsync(tenantId, exceptionId, ct) is { } x)
        {
            exception = new VisitEvidenceExceptionSummaryDto(x.Id, x.Rules.ToList(), x.StartDate, x.EndDate, x.ApprovedBy, x.ApprovedAt);
        }

        return Response<VisitEvidenceFlagDetailDto>.Success(new VisitEvidenceFlagDetailDto(row, exception));
    }
}
