using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Queries;
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
/// <para><b>Why the join happens here.</b> Position and unit labels are resolved server-side, exactly as the
/// position lookup does: a client-side merge across three collections is fragile, and a row without its unit
/// cannot distinguish two people holding the same position in different facilities (K4, transposed onto
/// people).</para>
///
/// <para><b>Names are best effort.</b> Every id is resolved in ONE batched call through
/// <see cref="IUserDisplayNameResolver"/>. If AuthService is unreachable the resolver returns nothing and this
/// handler still succeeds with <c>DisplayName = null</c> — a degraded picker beats a broken one.</para>
/// </summary>
public sealed class GetTaskAssignmentPersonLookupHandler
    : IRequestHandler<GetTaskAssignmentPersonLookupQuery, Response<IReadOnlyList<AssignablePersonDto>>>
{
    private readonly IPositionAssignmentRepository _positionAssignments;
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IUserDisplayNameResolver _displayNames;

    public GetTaskAssignmentPersonLookupHandler(
        IPositionAssignmentRepository positionAssignments,
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        IUserDisplayNameResolver displayNames)
    {
        _positionAssignments = positionAssignments;
        _positions = positions;
        _organizationUnits = organizationUnits;
        _displayNames = displayNames;
    }

    public async Task<Response<IReadOnlyList<AssignablePersonDto>>> Handle(
        GetTaskAssignmentPersonLookupQuery request,
        CancellationToken ct)
    {
        var assignments = await _positionAssignments.GetAllAsync(ct);
        var positions = await _positions.GetAllAsync(ct);
        var units = await _organizationUnits.GetAllAsync(ct);

        var positionById = positions.ToDictionary(p => p.Id);
        var unitById = units.ToDictionary(u => u.Id);
        var now = DateTimeOffset.UtcNow;

        // Half-open interval, consistent with the org-unit fallback and the position lookup.
        var active = assignments
            .Where(a => !a.IsCancelled
                        && a.EffectiveFrom <= now
                        && (a.EffectiveTo is null || a.EffectiveTo > now))
            // A primary assignment is the person's "home" position when they hold several.
            .OrderBy(a => a.AssignmentType)
            .ToList();

        var rows = new List<AssignablePersonDto>();
        var seenUsers = new HashSet<Guid>();

        foreach (var assignment in active)
        {
            // One row per person: a second position would duplicate the same human in the picker.
            if (!seenUsers.Add(assignment.UserId))
            {
                continue;
            }

            if (!positionById.TryGetValue(assignment.PositionId, out var position)
                || position.IsArchived
                || position.Status != PositionStatus.Active)
            {
                seenUsers.Remove(assignment.UserId);
                continue;
            }

            // Without the unit label the row cannot be told apart from a namesake elsewhere, so skip rather
            // than show an ambiguous entry.
            if (!unitById.TryGetValue(position.OrganizationUnitId, out var unit) || unit.IsArchived)
            {
                seenUsers.Remove(assignment.UserId);
                continue;
            }

            rows.Add(new AssignablePersonDto(
                UserId: assignment.UserId,
                DisplayName: null,
                PositionId: position.Id,
                PositionCode: position.Code,
                PositionName: position.Name,
                OrganizationUnitId: unit.Id,
                OrganizationUnitCode: unit.Code,
                OrganizationUnitName: unit.Name));
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

        return Response<IReadOnlyList<AssignablePersonDto>>.Success(
            ordered, correlationId: request.CorrelationId);
    }
}
