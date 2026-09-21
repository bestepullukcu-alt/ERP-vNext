using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Domain.Repositories;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;

public sealed class UpdateSupplierHandler : IRequestHandler<UpdateSupplierCommand, Response<SupplierDetailDto>>
{
    private readonly ISupplierRepository _repository;

    public UpdateSupplierHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<SupplierDetailDto>> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        // Cross-tenant / cross-LE → null → 404 (UNKNOWN_SUPPLIER; cross-LE write engellenir).
        var entity = await _repository.GetBySupplierIdAsync(request.SupplierId, cancellationToken);
        if (entity is null)
        {
            return Response<SupplierDetailDto>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        // ── Optimistic concurrency (If-Match/rowVersion): stale → 409, sessiz overwrite YOK ──
        if (request.ExpectedVersion.HasValue && request.ExpectedVersion.Value != entity.Version)
        {
            return Response<SupplierDetailDto>.Fail("CONCURRENCY_CONFLICT", 409);
        }

        var taxId = string.IsNullOrWhiteSpace(request.TaxId) ? null : request.TaxId!.Trim();

        // ── Duplicate aktif TaxId (tenant+LE, kendi kaydı hariç) → 409 ──
        if (taxId is not null
            && !string.Equals(taxId, entity.TaxId, StringComparison.Ordinal)
            && await _repository.ExistsByTaxIdAsync(taxId, entity.Id, cancellationToken))
        {
            return Response<SupplierDetailDto>.Fail("DUPLICATE_SUPPLIER", 409);
        }

        var expectedVersion = entity.Version;

        entity.Name = request.Name.Trim();
        entity.Country = CreateSupplierHandler.NormalizeCountry(request.Country);
        entity.TaxId = taxId;
        entity.Contacts = CreateSupplierHandler.MapContacts(request.Contacts);
        entity.SourceSystem = request.SourceSystem;
        entity.ExternalRef = request.ExternalRef;

        // Version filtresi ile uygula; arada değiştiyse (lost race) → 409.
        var ok = await _repository.UpdateAsync(entity, expectedVersion, cancellationToken);
        if (!ok)
        {
            return Response<SupplierDetailDto>.Fail("CONCURRENCY_CONFLICT", 409);
        }

        return Response<SupplierDetailDto>.Success(SupplierMapping.ToDetail(entity));
    }
}
