using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Queries;
using Diten.ProcurementService.Domain.Repositories;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.QueryHandlers;

public sealed class GetSupplierByIdHandler
    : IRequestHandler<GetSupplierByIdQuery, Response<SupplierDetailDto>>
{
    private readonly ISupplierRepository _repository;

    public GetSupplierByIdHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<SupplierDetailDto>> Handle(
        GetSupplierByIdQuery request,
        CancellationToken cancellationToken)
    {
        // Cross-tenant / cross-LE erişim → repository null döner → 404 (UNKNOWN_SUPPLIER; sızıntı yok).
        var entity = await _repository.GetBySupplierIdAsync(request.SupplierId, cancellationToken);
        if (entity is null)
        {
            return Response<SupplierDetailDto>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        return Response<SupplierDetailDto>.Success(SupplierMapping.ToDetail(entity));
    }
}
