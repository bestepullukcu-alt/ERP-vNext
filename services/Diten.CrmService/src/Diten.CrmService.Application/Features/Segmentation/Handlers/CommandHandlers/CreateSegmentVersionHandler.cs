using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;

/// <summary>
/// Clones a segment version into a new DRAFT. Two independent single-document writes (read, clone, insert) that can
/// never leave half a segment behind, so no transaction or compensation is needed.
/// <para>The clone gets <b>brand-new NodeIds with every ParentNodeId remapped</b> onto them. Without that remap the new
/// tree would still reference the previous version nodes, and an edit to one version would silently rewrite the other —
/// exactly the leak the freeze rule exists to prevent.</para>
/// </summary>
public sealed class CreateSegmentVersionHandler : IRequestHandler<CreateSegmentVersionCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ISegmentRepository _segments;

    public CreateSegmentVersionHandler(ITenantContext tenant, IActorContext actor, ISegmentRepository segments)
    {
        _tenant = tenant;
        _actor = actor;
        _segments = segments;
    }

    public async Task<Response<Guid>> Handle(
        CreateSegmentVersionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var source = await _segments.GetByIdAsync(tenantId, request.SegmentId, cancellationToken);
        if (source is null)
        {
            return Response<Guid>.Fail("Segment not found.", 404);
        }

        if (source.IsArchived())
        {
            return Response<Guid>.Fail("An archived segment cannot be versioned.", 409);
        }

        var lineage = await _segments.ListByLineageAsync(tenantId, source.VersionLineageId, cancellationToken);
        var nextVersion = (lineage.Count == 0 ? source.SegmentVersion : lineage.Max(s => s.SegmentVersion)) + 1;

        if (lineage.Any(s => s.SegmentVersion == nextVersion && !s.IsArchived()))
        {
            return Response<Guid>.Fail($"Version {nextVersion} of this segment already exists.", 409);
        }

        var clone = new Segment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SegmentCode = source.SegmentCode,
            SegmentName = source.SegmentName,
            SegmentType = source.SegmentType,
            SubjectType = source.SubjectType,
            SegmentStatus = SegmentStatuses.Draft,
            SegmentVersion = nextVersion,
            VersionLineageId = source.VersionLineageId,
            BusinessUnitId = source.BusinessUnitId,
            Description = source.Description,
            Notes = source.Notes,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            MatchMode = source.MatchMode,
            Criteria = SegmentMapper.CloneCriteria(source.Criteria),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _actor.ActorName
        };

        await _segments.InsertAsync(clone, cancellationToken);
        return Response<Guid>.Success(clone.Id, 201);
    }
}
