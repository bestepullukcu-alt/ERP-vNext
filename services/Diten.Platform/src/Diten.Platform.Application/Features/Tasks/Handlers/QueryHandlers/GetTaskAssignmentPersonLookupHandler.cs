using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>
/// MOD-0024 — people a task may be assigned to (pack §12 K6.4). Replaces the bare text input that demanded a
/// user GUID.
///
/// <para><b>Who is in the list.</b> Whoever holds a position right now: the active <c>PositionAssignment</c> set
/// (half-open interval, not cancelled), which the tenant-scoped repositories already restrict to this tenant.
/// Someone with no position is absent by design — the accepted cost of not exposing the whole directory.</para>
///
/// <para><b>BL-057 — and then the company scope.</b> Holding a position is necessary, not sufficient. An
/// ASSIGNMENT list is narrowed to <see cref="ITaskAssignmentScopeResolver"/>'s answer: same legal entity, below
/// me in the reporting chain, or an explicitly granted scope. A DECISION list (approver, reviewer) skips that
/// narrowing entirely — see <see cref="TaskPersonLookupPurpose"/> for why applying it there would silently kill
/// intra-group approval.</para>
///
/// <para><b>BL-072 — and it says who is missing.</b> Six reasons drop a candidate and the list used to report
/// none of them. Every distinct person who has an assignment but produced no row is counted, by reason, into
/// <see cref="ExcludedCandidateSummary"/>. Counts only: naming the out-of-scope people would return exactly what
/// the scope rule withholds.</para>
///
/// <para><b>Names are best effort.</b> Every id is resolved in ONE batched call through
/// <see cref="IUserDisplayNameResolver"/>. If AuthService is unreachable the resolver returns nothing and this
/// handler still succeeds with <c>DisplayName = null</c> — a degraded picker beats a broken one.</para>
/// </summary>
public sealed class GetTaskAssignmentPersonLookupHandler
    : IRequestHandler<GetTaskAssignmentPersonLookupQuery, Response<AssignablePersonLookupDto>>
{
    private readonly IPositionAssignmentRepository _positionAssignments;
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly ITaskAssignmentScopeResolver _scopes;

    public GetTaskAssignmentPersonLookupHandler(
        IPositionAssignmentRepository positionAssignments,
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        IUserDisplayNameResolver displayNames,
        ITaskAssignmentScopeResolver scopes)
    {
        _positionAssignments = positionAssignments;
        _positions = positions;
        _organizationUnits = organizationUnits;
        _displayNames = displayNames;
        _scopes = scopes;
    }

    /// <summary>Why a candidate did not make the list. Ordered most-severe-last so a person holding several
    /// positions is reported by their BEST outcome — "out of scope" beats "no active position", because the
    /// former means they were otherwise eligible.</summary>
    private enum SkipReason
    {
        NoActivePosition = 0,
        PositionNotActive = 1,
        OutOfScope = 2
    }

    public async Task<Response<AssignablePersonLookupDto>> Handle(
        GetTaskAssignmentPersonLookupQuery request,
        CancellationToken ct)
    {
        var assignments = await _positionAssignments.GetAllAsync(ct);
        var positions = await _positions.GetAllAsync(ct);
        var units = await _organizationUnits.GetAllAsync(ct);

        var positionById = positions.ToDictionary(p => p.Id);
        var unitById = units.ToDictionary(u => u.Id);
        var now = DateTimeOffset.UtcNow;

        // The scope is resolved ONCE and then asked per row. A DECISION list does not consult it at all.
        var scoped = request.Purpose == TaskPersonLookupPurpose.Assignment;
        var scope = scoped ? await _scopes.ResolveAsync(ct) : null;

        var rows = new List<AssignablePersonDto>();
        var listed = new HashSet<Guid>();
        // Best (lowest-severity) outcome per person, so someone with two positions is judged by the better one.
        var skipped = new Dictionary<Guid, SkipReason>();

        void Skip(Guid userId, SkipReason reason)
        {
            if (listed.Contains(userId)) { return; }
            if (!skipped.TryGetValue(userId, out var existing) || reason > existing)
            {
                skipped[userId] = reason;
            }
        }

        // Half-open interval, consistent with the org-unit fallback and the position lookup.
        var active = assignments
            .Where(a => !a.IsCancelled
                        && a.EffectiveFrom <= now
                        && (a.EffectiveTo is null || a.EffectiveTo > now))
            // A primary assignment is the person's "home" position when they hold several.
            .OrderBy(a => a.AssignmentType)
            .ToList();

        // Anyone with an assignment row at all is a CANDIDATE; whoever never reaches `rows` is reported as
        // excluded. Without this the "no active position" case would be invisible — those rows are filtered out
        // above, so the loop below never sees them.
        var candidates = assignments.Select(a => a.UserId).ToHashSet();

        foreach (var assignment in active)
        {
            // One row per person: a second position would duplicate the same human in the picker.
            if (listed.Contains(assignment.UserId))
            {
                continue;
            }

            if (!positionById.TryGetValue(assignment.PositionId, out var position)
                || position.IsArchived
                || position.Status != PositionStatus.Active)
            {
                Skip(assignment.UserId, SkipReason.PositionNotActive);
                continue;
            }

            // Without the unit label the row cannot be told apart from a namesake elsewhere, so skip rather
            // than show an ambiguous entry.
            if (!unitById.TryGetValue(position.OrganizationUnitId, out var unit) || unit.IsArchived)
            {
                Skip(assignment.UserId, SkipReason.PositionNotActive);
                continue;
            }

            // BL-057. The one line the whole round is about, and it is asked of the shared rule rather than
            // re-derived here.
            if (scope is not null
                && !scope.Allows(position.Id, unit.Id, unit.LegalEntityId))
            {
                Skip(assignment.UserId, SkipReason.OutOfScope);
                continue;
            }

            listed.Add(assignment.UserId);
            skipped.Remove(assignment.UserId);

            rows.Add(new AssignablePersonDto(
                UserId: assignment.UserId,
                DisplayName: null,
                PositionId: position.Id,
                PositionCode: position.Code,
                PositionName: position.Name,
                OrganizationUnitId: unit.Id,
                OrganizationUnitCode: unit.Code,
                OrganizationUnitName: unit.Name,
                LegalEntityId: unit.LegalEntityId));
        }

        // Everyone who has an assignment somewhere but never produced a row, and was not already explained by
        // one of the loop's reasons: their assignments were all expired, future-dated or cancelled.
        foreach (var userId in candidates)
        {
            if (!listed.Contains(userId) && !skipped.ContainsKey(userId))
            {
                skipped[userId] = SkipReason.NoActivePosition;
            }
        }

        // ONE call for every id, not one per row.
        var names = await _displayNames.ResolveAsync(rows.Select(r => r.UserId).ToList(), ct);

        IReadOnlyList<AssignablePersonDto> ordered = rows
            .Select(row => names.TryGetValue(row.UserId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? row with { DisplayName = name }
                : row)
            // Named people first and alphabetically; the unresolved tail stays grouped by unit/position.
            .OrderBy(row => row.DisplayName is null)
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.OrganizationUnitName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.PositionName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var excluded = new ExcludedCandidateSummary(
            Total: skipped.Count,
            NoActivePosition: skipped.Count(e => e.Value == SkipReason.NoActivePosition),
            PositionNotActive: skipped.Count(e => e.Value == SkipReason.PositionNotActive),
            OutOfScope: skipped.Count(e => e.Value == SkipReason.OutOfScope));

        return Response<AssignablePersonLookupDto>.Success(
            new AssignablePersonLookupDto(ordered, excluded), correlationId: request.CorrelationId);
    }
}
