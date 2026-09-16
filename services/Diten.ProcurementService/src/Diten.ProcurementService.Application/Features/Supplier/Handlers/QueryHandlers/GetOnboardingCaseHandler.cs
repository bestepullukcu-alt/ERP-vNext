using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Queries;
using Diten.ProcurementService.Domain.Repositories;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.QueryHandlers;

public sealed class GetOnboardingCaseHandler
    : IRequestHandler<GetOnboardingCaseQuery, Response<OnboardingCaseDto>>
{
    private readonly ISupplierRepository _repository;

    public GetOnboardingCaseHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<OnboardingCaseDto>> Handle(
        GetOnboardingCaseQuery request,
        CancellationToken cancellationToken)
    {
        // Cross-tenant / cross-LE → null → 404 (UNKNOWN_SUPPLIER; sızıntı yok).
        var entity = await _repository.GetBySupplierIdAsync(request.SupplierId, cancellationToken);
        if (entity is null)
        {
            return Response<OnboardingCaseDto>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        return Response<OnboardingCaseDto>.Success(SupplierMapping.ToOnboardingCase(entity));
    }
}
