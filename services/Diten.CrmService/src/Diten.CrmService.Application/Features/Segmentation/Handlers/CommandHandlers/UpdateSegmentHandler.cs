using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;

/// <summary>
/// Updates a segment. Three things it deliberately cannot do: change <c>SubjectType</c> (immutable — a segment must not
/// silently start answering a different question), move the lifecycle (activate and archive are their own endpoints,
/// with their own permissions), or edit the criteria of an ACTIVE segment (frozen — the change belongs in a new
/// version, so past explanations stay true).
/// <para>Sending the SAME criteria tree back is not a change and is accepted: the freeze guard compares the STRUCTURE
/// of the rule, not the ids it arrived with.</para>
/// </summary>
public sealed class UpdateSegmentHandler : IRequestHandler<UpdateSegmentCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ISegmentRepository _segments;
    private readonly ISegmentProductReferenceValidator _references;

    public UpdateSegmentHandler(
        ITenantContext tenant,
        IActorContext actor,
        ISegmentRepository segments,
        ISegmentProductReferenceValidator references)
    {
        _tenant = tenant;
        _actor = actor;
        _segments = segments;
        _references = references;
    }

    public async Task<Response<bool>> Handle(UpdateSegmentCommand request, CancellationToken cancellationToken)
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

        if (segment.IsArchived())
        {
            return Response<bool>.Fail("An archived segment cannot be updated.", 409);
        }

        var requestedStatus = SegmentStatuses.Normalize(request.SegmentStatus);
        if (!string.Equals(requestedStatus, segment.SegmentStatus, StringComparison.Ordinal))
        {
            return Response<bool>.Fail(
                "SegmentStatus is not changed through update; use the activate or archive endpoint "
                + "(they carry their own permissions).", 400);
        }

        var criteria = request.CriteriaProvided
            ? SegmentMapper.ToCriteria(request.Criteria)
            : segment.Criteria;

        var shapeFailure = SegmentWriteGuards.ValidateSegmentShape(
            request.SegmentName, request.SegmentType, segment.SubjectType, request.MatchMode,
            request.EffectiveFrom, request.EffectiveTo, request.BusinessUnitId, request.Description,
            request.Notes, criteria);
        if (shapeFailure is not null)
        {
            return Response<bool>.Fail(SegmentWriteGuards.ToErrors(shapeFailure), shapeFailure.StatusCode);
        }

        var criteriaChanged = request.CriteriaProvided
                              && SegmentMapper.CriteriaDiffer(segment.Criteria, criteria);

        if (segment.IsCriteriaFrozen() && criteriaChanged)
        {
            var frozen = new SegmentValidation.Failure(
                "This segment version is active and its criteria are frozen. Create a new version to change the rule.",
                SegmentErrorCodes.CriteriaFrozen, 409);
            return Response<bool>.Fail(SegmentWriteGuards.ToErrors(frozen), 409);
        }

        if (criteriaChanged)
        {
            var referenceFailure = await SegmentWriteGuards.ValidateCrossServiceReferencesAsync(
                _references, criteria, cancellationToken);
            if (referenceFailure is not null)
            {
                return Response<bool>.Fail(
                    SegmentWriteGuards.ToErrors(referenceFailure), referenceFailure.StatusCode);
            }
        }

        var expectedVersion = request.ExpectedVersion ?? segment.Version;

        segment.SegmentName = request.SegmentName.Trim();
        segment.SegmentType = SegmentTypes.Normalize(request.SegmentType);
        segment.MatchMode = SegmentMatchModes.Normalize(request.MatchMode);
        segment.EffectiveFrom = request.EffectiveFrom;
        segment.EffectiveTo = request.EffectiveTo;
        segment.BusinessUnitId = SegmentValidation.Trim(request.BusinessUnitId);
        segment.Description = SegmentValidation.Trim(request.Description);
        segment.Notes = SegmentValidation.Trim(request.Notes);
        segment.UpdatedAt = DateTimeOffset.UtcNow;
        segment.UpdatedBy = _actor.ActorName;
        if (criteriaChanged)
        {
            segment.Criteria = criteria;
        }

        var replaced = await _segments.ReplaceAsync(segment, expectedVersion, cancellationToken);
        return replaced
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The segment changed since it was loaded. Reload and try again.", 409);
    }
}
