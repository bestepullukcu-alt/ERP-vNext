using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSets;

public sealed class ListContentSetsHandler : IRequestHandler<ListContentSetsQuery, Response<ContentSetListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContentSetRepository _sets;

    public ListContentSetsHandler(ITenantContext tenant, IContentSetRepository sets)
    {
        _tenant = tenant;
        _sets = sets;
    }

    public async Task<Response<ContentSetListDto>> Handle(ListContentSetsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContentSetListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<ContentSet> rows = await _sets.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ContentSetStatuses.Normalize(request.Status);
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(x =>
                x.SetName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.SetCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var items = rows.Select(ContentSetMapper.ToDto).ToList();
        return Response<ContentSetListDto>.Success(new ContentSetListDto(items, items.Count));
    }
}

public sealed class GetContentSetHandler : IRequestHandler<GetContentSetQuery, Response<ContentSetDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContentSetRepository _sets;

    public GetContentSetHandler(ITenantContext tenant, IContentSetRepository sets)
    {
        _tenant = tenant;
        _sets = sets;
    }

    public async Task<Response<ContentSetDto>> Handle(GetContentSetQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContentSetDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _sets.GetByIdAsync(tenantId, request.ContentSetId, cancellationToken);
        return entity is null
            ? Response<ContentSetDto>.Fail("Content set not found.", 404)
            : Response<ContentSetDto>.Success(ContentSetMapper.ToDto(entity));
    }
}
