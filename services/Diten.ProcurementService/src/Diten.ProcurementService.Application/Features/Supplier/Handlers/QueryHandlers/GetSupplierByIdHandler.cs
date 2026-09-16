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
        // Cross-tenant / cross-LE erişim → repository null döner → 404 (sızıntı yok).
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null)
        {
            return Response<SupplierDetailDto>.Fail("Record not found.", 404);
        }

        var contacts = entity.Contacts
            .Select(c => new SupplierContactDto(c.Type, c.Email, c.Phone, c.Name))
            .ToList();

        var dto = new SupplierDetailDto(
            entity.Id,
            entity.SupplierId,
            entity.Name,
            entity.Status,
            entity.Country,
            entity.TaxId,
            contacts,
            entity.OnboardingStatus,
            entity.KycOutcome,
            entity.SanctionsOutcome,
            entity.SourceSystem,
            entity.ExternalRef);

        return Response<SupplierDetailDto>.Success(dto);
    }
}
