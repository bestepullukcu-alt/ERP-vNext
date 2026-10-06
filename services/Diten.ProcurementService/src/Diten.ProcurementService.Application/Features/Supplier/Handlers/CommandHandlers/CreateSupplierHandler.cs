using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using MediatR;
using SupplierEntity = Diten.ProcurementService.Domain.Entities.Supplier;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;

public sealed class CreateSupplierHandler : IRequestHandler<CreateSupplierCommand, Response<SupplierDetailDto>>
{
    private readonly ISupplierRepository _repository;

    public CreateSupplierHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<SupplierDetailDto>> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        // ── Idempotent create (MOD-0140 §8): aynı Idempotency-Key ile replay → mevcut kaydı döner, yeni yaratmaz ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var replay = await _repository.GetByIdempotencyKeyAsync(request.IdempotencyKey!, cancellationToken);
            if (replay is not null)
            {
                return Response<SupplierDetailDto>.Success(SupplierMapping.ToDetail(replay), 201);
            }
        }

        var taxId = string.IsNullOrWhiteSpace(request.TaxId) ? null : request.TaxId!.Trim();

        // ── Duplicate aktif TaxId (tenant+LE) → 409 DUPLICATE_SUPPLIER ──
        if (taxId is not null && await _repository.ExistsByTaxIdAsync(taxId, null, cancellationToken))
        {
            return Response<SupplierDetailDto>.Fail("DUPLICATE_SUPPLIER", 409);
        }

        var entity = new SupplierEntity
        {
            SupplierId = GenerateSupplierId(),
            Name = request.Name.Trim(),
            Status = SupplierStatus.Active,
            Country = NormalizeCountry(request.Country),
            TaxId = taxId,
            Contacts = MapContacts(request.Contacts),
            OnboardingStatus = OnboardingStatus.Draft,
            KycOutcome = KycOutcome.Pending,
            SanctionsOutcome = SanctionsOutcome.Pending,
            SourceSystem = request.SourceSystem,
            ExternalRef = request.ExternalRef,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);
        return Response<SupplierDetailDto>.Success(SupplierMapping.ToDetail(created), 201);
    }

    internal static List<SupplierContact> MapContacts(List<SupplierContactInput>? contacts)
        => (contacts ?? new List<SupplierContactInput>())
            .Select(c => new SupplierContact { Type = c.Type, Email = c.Email, Phone = c.Phone, Name = c.Name })
            .ToList();

    internal static string? NormalizeCountry(string? country)
        => string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToLowerInvariant();

    private static string GenerateSupplierId()
        => "SUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
