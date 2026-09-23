using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.Rendering;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;

public sealed class GetContentSetRevisionByIdHandler
    : IRequestHandler<GetContentSetRevisionByIdQuery, Response<ContentSetRevisionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContentSetRevisionRepository _revisions;

    public GetContentSetRevisionByIdHandler(ITenantContext tenant, IContentSetRevisionRepository revisions)
    {
        _tenant = tenant;
        _revisions = revisions;
    }

    public async Task<Response<ContentSetRevisionDto>> Handle(
        GetContentSetRevisionByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContentSetRevisionDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _revisions.GetByIdAsync(tenantId, request.RevisionId, cancellationToken);
        return entity is null
            ? Response<ContentSetRevisionDto>.Fail("Content set revision not found.", 404)
            : Response<ContentSetRevisionDto>.Success(ContentSetRevisionMapper.ToDto(entity));
    }
}

public sealed class ListContentSetRevisionsHandler
    : IRequestHandler<ListContentSetRevisionsQuery, Response<ContentSetRevisionListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContentSetRevisionRepository _revisions;

    public ListContentSetRevisionsHandler(ITenantContext tenant, IContentSetRevisionRepository revisions)
    {
        _tenant = tenant;
        _revisions = revisions;
    }

    public async Task<Response<ContentSetRevisionListDto>> Handle(
        ListContentSetRevisionsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContentSetRevisionListDto>.Fail("Tenant context is required.", 400);
        }

        var rows = await _revisions.ListByContentSetAsync(tenantId, request.ContentSetId, cancellationToken);
        var items = rows.Select(ContentSetRevisionMapper.ToDto).ToList();
        return Response<ContentSetRevisionListDto>.Success(new ContentSetRevisionListDto(items, items.Count));
    }
}

/// <summary>
/// SCMM-16B — opens the rendered artifact stream for a revision. The content id is resolved from the revision's bound
/// artifact, never taken from the client (FU01 non-leakage preserved). An unknown/other-tenant revision or an unrendered
/// revision is 404; an absent object at FU01 (e.g. a cross-tenant content id) also surfaces as 404.
/// </summary>
public sealed class GetContentSetRevisionArtifactHandler
    : IRequestHandler<GetContentSetRevisionArtifactQuery, Response<ContentArtifactReadResult>>
{
    private readonly ITenantContext _tenant;
    private readonly IContentSetRevisionRepository _revisions;
    private readonly IContentArtifactStore _store;

    public GetContentSetRevisionArtifactHandler(
        ITenantContext tenant, IContentSetRevisionRepository revisions, IContentArtifactStore store)
    {
        _tenant = tenant;
        _revisions = revisions;
        _store = store;
    }

    public async Task<Response<ContentArtifactReadResult>> Handle(
        GetContentSetRevisionArtifactQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContentArtifactReadResult>.Fail("Tenant context is required.", 400);
        }

        var revision = await _revisions.GetByIdAsync(tenantId, request.RevisionId, cancellationToken);
        if (revision?.RenderedArtifact is not { } artifact)
        {
            // Non-leakage: an unknown/other-tenant revision and a not-yet-rendered revision are indistinguishable — 404.
            return Response<ContentArtifactReadResult>.Fail("Rendered artifact not found.", 404);
        }

        var stream = await _store.OpenReadAsync(artifact.ContentId, cancellationToken);
        return stream is null
            ? Response<ContentArtifactReadResult>.Fail("Rendered artifact not found.", 404)
            : Response<ContentArtifactReadResult>.Success(stream);
    }
}
