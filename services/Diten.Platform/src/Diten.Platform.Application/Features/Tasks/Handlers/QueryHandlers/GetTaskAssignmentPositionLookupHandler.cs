using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>
/// MOD-0024 — assignable positions for pool tasks (pack §12 K4).
///
/// <para>Two deliberate differences from the existing <c>GetPositionsQuery</c>, both of which are real defects if
/// omitted:</para>
/// <list type="number">
/// <item><b>The organization unit label is joined server-side.</b> <c>PositionDto</c> carries only
/// <c>OrganizationUnitId</c>, so every current picker renders bare "{code} {name}" and cannot distinguish
/// "QA Specialist — Facility A" from "QA Specialist — Facility B". Since a Position is always unit-bound
/// (<c>Position.OrganizationUnitId</c> is required), the unit label is what makes a pool choice unambiguous —
/// pooling to the wrong unit silently routes work to the wrong facility.</item>
/// <item><b>Draft and archived positions are excluded.</b> <c>Position.Status</c> defaults to <c>Draft</c> and
/// the existing handler applies no status/archive filter, so an unfiltered list offers positions that are not
/// real yet.</item>
/// </list>
/// </summary>
public sealed class GetTaskAssignmentPositionLookupHandler
    : IRequestHandler<GetTaskAssignmentPositionLookupQuery, Response<IReadOnlyList<AssignablePositionDto>>>
{
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IPositionAssignmentRepository _positionAssignments;
    private readonly ITaskAssignmentScopeResolver _scopes;

    public GetTaskAssignmentPositionLookupHandler(
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        IPositionAssignmentRepository positionAssignments,
        ITaskAssignmentScopeResolver scopes)
    {
        _positions = positions;
        _organizationUnits = organizationUnits;
        _positionAssignments = positionAssignments;
        _scopes = scopes;
    }

    public async Task<Response<IReadOnlyList<AssignablePositionDto>>> Handle(
        GetTaskAssignmentPositionLookupQuery request,
        CancellationToken ct)
    {
        var positions = await _positions.GetAllAsync(ct);
        var units = await _organizationUnits.GetAllAsync(ct);
        var assignments = await _positionAssignments.GetAllAsync(ct);

        var unitById = units.ToDictionary(u => u.Id);
        var now = DateTimeOffset.UtcNow;

        // BL-057 — the SAME rule the people picker uses, from the same place. Pooling work is assigning it to
        // whoever holds the position, so a pool outside my scope is the same boundary crossing as a person
        // outside it. Written twice these two would drift, and the drift is invisible: one picker narrows, the
        // other stays wide, and pooled work reaches a company the actor may not reach directly.
        var scope = await _scopes.ResolveAsync(ct);

        // Half-open interval, consistent with OrgDataScopeResolver / TenantOrganizationMapper.
        var holderCounts = assignments
            .Where(a => !a.IsCancelled
                        && a.EffectiveFrom <= now
                        && (a.EffectiveTo is null || a.EffectiveTo > now))
            .GroupBy(a => a.PositionId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.UserId).Distinct().Count());

        var result = new List<AssignablePositionDto>();
        foreach (var position in positions)
        {
            // Only genuinely usable positions may receive pooled work.
            if (position.IsArchived || position.Status != PositionStatus.Active)
            {
                continue;
            }

            // A position whose unit cannot be resolved is skipped rather than shown without its facility label:
            // an unlabelled pool entry is exactly how work reaches the wrong facility.
            if (!unitById.TryGetValue(position.OrganizationUnitId, out var unit) || unit.IsArchived)
            {
                continue;
            }

            if (!scope.Allows(position.Id, unit.Id, unit.LegalEntityId))
            {
                continue;
            }

            result.Add(new AssignablePositionDto(
                PositionId: position.Id,
                PositionCode: position.Code,
                PositionName: position.Name,
                OrganizationUnitId: unit.Id,
                OrganizationUnitCode: unit.Code,
                OrganizationUnitName: unit.Name,
                LegalEntityId: unit.LegalEntityId,
                ActiveHolderCount: holderCounts.TryGetValue(position.Id, out var count) ? count : 0));
        }

        IReadOnlyList<AssignablePositionDto> ordered = result
            .OrderBy(r => r.OrganizationUnitName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.PositionName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Response<IReadOnlyList<AssignablePositionDto>>.Success(
            ordered, correlationId: request.CorrelationId);
    }
}
