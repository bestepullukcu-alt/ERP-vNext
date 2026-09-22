using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
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
