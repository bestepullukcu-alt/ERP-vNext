using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Nodes.Handlers;

/// <summary>WP-SEG-DETAILS8 — bulk node reverse lookup. Given a set of node ids (from another aggregate that stored only
/// the id, e.g. a segment criterion), returns {id, name, modelId, code} for the tenant's own nodes. It is a pure READ
/// over <c>territory_nodes</c> and cannot write anything; the territory aggregate/write path is untouched.
/// <para>Fail-closed by construction: no tenant context is a 400, and an empty / unparsable id list is an empty result
/// (never a query for every node). Ids that do not resolve are simply absent from the result — a consumer keeps the raw
/// id rather than being handed a fabricated name.</para></summary>
public sealed class GetTerritoryNodesByIdsHandler
    : IRequestHandler<GetTerritoryNodesByIdsQuery, Response<IReadOnlyList<TerritoryNodeLookupDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryNodeRepository _nodes;

    public GetTerritoryNodesByIdsHandler(ITenantContext tenant, ITerritoryNodeRepository nodes)
    {
        _tenant = tenant;
        _nodes = nodes;
    }

    public async Task<Response<IReadOnlyList<TerritoryNodeLookupDto>>> Handle(
        GetTerritoryNodesByIdsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<TerritoryNodeLookupDto>>.Fail("Tenant context is required.", 400);
        }

        var ids = ParseIds(request.Ids);
        if (ids.Count == 0)
        {
            return Response<IReadOnlyList<TerritoryNodeLookupDto>>.Success(Array.Empty<TerritoryNodeLookupDto>());
        }

        var nodes = await _nodes.ListByIdsAsync(tenantId, ids, cancellationToken);
        var result = (IReadOnlyList<TerritoryNodeLookupDto>)nodes
            .Select(n => new TerritoryNodeLookupDto(n.Id, n.Name, n.ModelId, n.TerritoryCode))
            .ToList();
        return Response<IReadOnlyList<TerritoryNodeLookupDto>>.Success(result);
    }

    private static IReadOnlyCollection<Guid> ParseIds(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<Guid>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => Guid.TryParse(part, out var id) ? id : (Guid?)null)
                .Where(id => id is { } value && value != Guid.Empty)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
}
