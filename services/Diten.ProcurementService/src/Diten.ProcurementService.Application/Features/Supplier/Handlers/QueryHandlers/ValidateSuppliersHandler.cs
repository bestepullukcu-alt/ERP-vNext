using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Queries;
using Diten.ProcurementService.Domain.Repositories;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.QueryHandlers;

public sealed class ValidateSuppliersHandler
    : IRequestHandler<ValidateSuppliersQuery, Response<SupplierValidateResponseDto>>
{
    private readonly ISupplierRepository _repository;

    public ValidateSuppliersHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<SupplierValidateResponseDto>> Handle(
        ValidateSuppliersQuery request,
        CancellationToken cancellationToken)
    {
        var requestedIds = (request.SupplierIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Tenant+LE filtreli — başka tenant/LE'nin id'si burada bilinmez → known:false (fail-closed, sızıntı yok).
        var known = await _repository.GetBySupplierIdsAsync(requestedIds, cancellationToken);
        var knownMap = known.ToDictionary(x => x.SupplierId, x => x, StringComparer.Ordinal);

        var results = requestedIds
            .Select(id => knownMap.TryGetValue(id, out var s)
                ? new SupplierValidationResultDto(id, true, s.Status)
                : new SupplierValidationResultDto(id, false, null))
            .ToList();

        var response = new SupplierValidateResponseDto(results, SupplierContract.Version);
        return Response<SupplierValidateResponseDto>.Success(response);
    }
}
