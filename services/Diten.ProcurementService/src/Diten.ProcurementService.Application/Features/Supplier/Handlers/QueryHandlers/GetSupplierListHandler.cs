using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Queries;
using Diten.ProcurementService.Domain.Repositories;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.QueryHandlers;

public sealed class GetSupplierListHandler
    : IRequestHandler<GetSupplierListQuery, Response<SupplierListResultDto>>
{
    // Cursor sayfa boyutu (contract listSuppliers cursor). Sabit; policy netleşince config'e taşınır.
    private const int PageSize = 50;

    private readonly ISupplierRepository _repository;

    public GetSupplierListHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<SupplierListResultDto>> Handle(
        GetSupplierListQuery request,
        CancellationToken cancellationToken)
    {
        // Repository, Tenant + LegalEntity + IsDeleted=false ile filtreler (cross-LE/tenant sızıntısı yok).
        var entities = await _repository.GetAllAsync(request.Status, cancellationToken);

        // Cursor = son dönen SupplierId (opaque). Kararlı sıralama Name (repo) → SupplierId ikincil.
        var ordered = entities
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.SupplierId, StringComparer.Ordinal)
            .ToList();

        IEnumerable<Domain.Entities.Supplier> page = ordered;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            page = ordered.Where(x => string.CompareOrdinal(x.SupplierId, request.Cursor) > 0);
        }

        var pageList = page.Take(PageSize + 1).ToList();
        var hasMore = pageList.Count > PageSize;
        var items = pageList.Take(PageSize).Select(SupplierMapping.ToListItem).ToList();
        var nextCursor = hasMore ? pageList[PageSize - 1].SupplierId : null;

        var result = new SupplierListResultDto(items, nextCursor, SupplierContract.Version);
        return Response<SupplierListResultDto>.Success(result);
    }
}
