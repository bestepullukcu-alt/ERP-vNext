using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;

/// <summary>
/// Creates a segment. It is always born <c>draft</c>, at business version 1, as the root of its own lineage: a rule is
/// never born live, because putting one live is a separate act with a separate permission.
/// <para>Order matters here. The criteria tree is validated in-domain, then every cross-service value in it is PROVEN,
/// and only then is anything written — so a 503 from the reference master leaves no half-authored segment behind.</para>
/// </summary>
public sealed class CreateSegmentHandler : IRequestHandler<CreateSegmentCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ISegmentRepository _segments;
    private readonly ISegmentProductReferenceValidator _references;

    public CreateSegmentHandler(
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

    public async Task<Response<Guid>> Handle(CreateSegmentCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var codeFailure = SegmentValidation.ValidateSegmentCode(request.SegmentCode);
        if (codeFailure is not null)
        {
            return Response<Guid>.Fail(SegmentWriteGuards.ToErrors(codeFailure), codeFailure.StatusCode);
        }

        var criteria = SegmentMapper.ToCriteria(request.Criteria);
        var shapeFailure = SegmentWriteGuards.ValidateSegmentShape(
            request.SegmentName, request.SegmentType, request.SubjectType, request.MatchMode,
            request.EffectiveFrom, request.EffectiveTo, request.BusinessUnitId, request.Description,
            request.Notes, criteria);
        if (shapeFailure is not null)
        {
            return Response<Guid>.Fail(SegmentWriteGuards.ToErrors(shapeFailure), shapeFailure.StatusCode);
        }

        var code = request.SegmentCode.Trim().ToLowerInvariant();
        var existing = await _segments.ListByCodeAsync(tenantId, code, cancellationToken);
        if (existing.Any(s => !s.IsArchived()))
        {
            return Response<Guid>.Fail($"A live segment already uses SegmentCode '{code}'.", 409);
        }

        // Cross-service proof BEFORE the insert: on 503 nothing is persisted at all.
        var referenceFailure = await SegmentWriteGuards.ValidateCrossServiceReferencesAsync(
            _references, criteria, cancellationToken);
        if (referenceFailure is not null)
        {
            return Response<Guid>.Fail(
                SegmentWriteGuards.ToErrors(referenceFailure), referenceFailure.StatusCode);
        }

        var id = Guid.NewGuid();
        var entity = new Segment
        {
            Id = id,
            TenantId = tenantId,
            SegmentCode = code,
            SegmentName = request.SegmentName.Trim(),
            SegmentType = SegmentTypes.Normalize(request.SegmentType),
            SubjectType = SegmentSubjectTypes.Normalize(request.SubjectType),
            SegmentStatus = SegmentStatuses.Draft,
            SegmentVersion = 1,
            VersionLineageId = id,
            BusinessUnitId = SegmentValidation.Trim(request.BusinessUnitId),
            Description = SegmentValidation.Trim(request.Description),
            Notes = SegmentValidation.Trim(request.Notes),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            MatchMode = SegmentMatchModes.Normalize(request.MatchMode),
            Criteria = criteria,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _actor.ActorName
        };

        await _segments.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
