using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Queries;
using Diten.ProcurementService.Domain.Repositories;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.QueryHandlers;

public sealed class GetSupplierListHandler
    : IRequestHandler<GetSupplierListQuery, Response<IReadOnlyList<SupplierListItemDto>>>
{
    private readonly ISupplierRepository _repository;

    public GetSupplierListHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<SupplierListItemDto>>> Handle(
        GetSupplierListQuery request,
        CancellationToken cancellationToken)
    {
        // Repository, Tenant + LegalEntity + IsDeleted=false ile filtreler; boş liste dönebilir (FAZ 1).
        var entities = await _repository.GetAllAsync(cancellationToken);
        var list = entities
            .Select(x => new SupplierListItemDto(
                x.Id,
                x.SupplierId,
                x.Name,
                x.Status,
                x.Country,
                x.TaxId,
                x.OnboardingStatus))
            .ToList();

        return Response<IReadOnlyList<SupplierListItemDto>>.Success(list);
    }
}
