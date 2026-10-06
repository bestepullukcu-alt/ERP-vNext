using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentScopes;

public sealed class ListContentScopesHandler : IRequestHandler<ListContentScopesQuery, Response<ContentScopeListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContentScopeRepository _scopes;

    public ListContentScopesHandler(ITenantContext tenant, IContentScopeRepository scopes)
    {
        _tenant = tenant;
        _scopes = scopes;
    }

    public async Task<Response<ContentScopeListDto>> Handle(
        ListContentScopesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContentScopeListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<ContentScope> rows = await _scopes.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ContentScopeStatuses.Normalize(request.Status);
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(x =>
                x.ScopeName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.ScopeCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var items = rows.Select(ContentScopeMapper.ToDto).ToList();
        return Response<ContentScopeListDto>.Success(new ContentScopeListDto(items, items.Count));
    }
}

public sealed class GetContentScopeHandler : IRequestHandler<GetContentScopeQuery, Response<ContentScopeDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContentScopeRepository _scopes;

    public GetContentScopeHandler(ITenantContext tenant, IContentScopeRepository scopes)
    {
        _tenant = tenant;
        _scopes = scopes;
    }

    public async Task<Response<ContentScopeDto>> Handle(
        GetContentScopeQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContentScopeDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _scopes.GetByIdAsync(tenantId, request.ContentScopeId, cancellationToken);
        return entity is null
            ? Response<ContentScopeDto>.Fail("Content scope not found.", 404)
            : Response<ContentScopeDto>.Success(ContentScopeMapper.ToDto(entity));
    }
}
