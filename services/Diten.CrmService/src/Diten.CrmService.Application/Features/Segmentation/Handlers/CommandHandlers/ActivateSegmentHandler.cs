using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;

/// <summary>
/// Puts a draft rule live and freezes its criteria. Two things happen and both are deliberate.
/// <para><b>The criteria freeze</b> is what makes a resolution auditable: a result can only be justified by a
/// (SegmentId, SegmentVersion) pair if that pair still asks the same question later. Editing a live rule in place
/// would silently invalidate every past explanation.</para>
/// <para><b>The predecessor is superseded, not archived.</b> Its <c>SupersededBySegmentId</c> is filled in and it stays
/// resolvable, because the honest answer to "why was this person selected back then?" needs the rule that was in force
/// back then.</para>
/// </summary>
public sealed class ActivateSegmentHandler : IRequestHandler<ActivateSegmentCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ISegmentRepository _segments;

    public ActivateSegmentHandler(ITenantContext tenant, IActorContext actor, ISegmentRepository segments)
    {
        _tenant = tenant;
        _actor = actor;
        _segments = segments;
    }

    public async Task<Response<bool>> Handle(ActivateSegmentCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var segment = await _segments.GetByIdAsync(tenantId, request.SegmentId, cancellationToken);
        if (segment is null)
        {
            return Response<bool>.Fail("Segment not found.", 404);
        }

        var transition = SegmentValidation.ValidateStatusTransition(segment.SegmentStatus, SegmentStatuses.Active);
        if (transition is not null)
        {
            return Response<bool>.Fail(transition.Message, transition.StatusCode);
        }

        if (string.Equals(segment.SegmentStatus, SegmentStatuses.Active, StringComparison.Ordinal))
        {
            return Response<bool>.Fail("The segment is already active.", 409);
        }

        var criteriaFailure = SegmentValidation.ValidateCriteria(
            segment.SegmentType, segment.SubjectType, segment.Criteria);
        if (criteriaFailure is not null)
        {
            return Response<bool>.Fail(
                SegmentWriteGuards.ToErrors(criteriaFailure), criteriaFailure.StatusCode);
        }

        var now = DateTimeOffset.UtcNow;
        var expectedVersion = request.ExpectedVersion ?? segment.Version;

        segment.SegmentStatus = SegmentStatuses.Active;
        segment.ActivatedAt = now;
        segment.ActivatedBy = _actor.ActorName;
        segment.CriteriaFrozenAt = now;
        segment.UpdatedAt = now;
        segment.UpdatedBy = _actor.ActorName;

        var replaced = await _segments.ReplaceAsync(segment, expectedVersion, cancellationToken);
        if (!replaced)
        {
            return Response<bool>.Fail("The segment changed since it was loaded. Reload and try again.", 409);
        }

        await SupersedePredecessorsAsync(tenantId, segment, now, cancellationToken);
        return Response<bool>.Success(true);
    }

    private async Task SupersedePredecessorsAsync(
        Guid tenantId, Segment activated, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lineage = await _segments.ListByLineageAsync(tenantId, activated.VersionLineageId, cancellationToken);

        foreach (var predecessor in lineage.Where(s =>
                     s.Id != activated.Id
                     && s.SegmentVersion < activated.SegmentVersion
                     && !s.IsArchived()
                     && s.SupersededBySegmentId is null
                     && string.Equals(s.SegmentStatus, SegmentStatuses.Active, StringComparison.Ordinal)))
        {
            predecessor.SupersededBySegmentId = activated.Id;
            predecessor.UpdatedAt = now;
            predecessor.UpdatedBy = _actor.ActorName;

            // Superseded, NOT archived: the old version keeps resolving so history stays explainable. A losing race
            // here is harmless - the next activation marks it, and the flag is derived state, not the rule itself.
            await _segments.ReplaceAsync(predecessor, predecessor.Version, cancellationToken);
        }
    }
}
