using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Application.Features.Supplier.Store;
using Diten.ProcurementService.Domain.Entities;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;

/// <summary>
/// "Link or create" (K-6, §12): a supplier the tenant already has (same country + normalized tax number, in ANY company
/// under <c>TenantShared</c>) is not created again — 409 <c>DUPLICATE_SUPPLIER</c> naming its code
/// (<c>errorDetails.supplierId</c>, Revizyon 9.1 F-1) so the screen can offer "link it to this company"; no silent link.
/// </summary>
public sealed class CreateSupplierHandler(ISupplierStore store) : IRequestHandler<CreateSupplierCommand, Response<SupplierDetailDto>>
{
    public async Task<Response<SupplierDetailDto>> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        // ── Idempotent create (MOD-0140 §8): aynı Idempotency-Key ile replay → mevcut kaydı döner, yeni yaratmaz ──
        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;
        if (key is not null && await store.FindByIdempotencyKeyAsync(key, cancellationToken) is { } replay)
        {
            return Response<SupplierDetailDto>.Success(SupplierMapping.ToDetail(replay), 201);
        }

        var country = SupplierWriteRules.NormalizeCountry(request.Country);
        var taxId = SupplierWriteRules.NormalizeTaxId(request.TaxId);
        if (taxId is not null && await store.FindDuplicateTaxIdAsync(country, taxId, null, cancellationToken) is { } existing)
        {
            return Duplicate(existing);
        }

        var now = DateTimeOffset.UtcNow;
        var draft = new SupplierSnapshot
        {
            SupplierId = SupplierWriteRules.GenerateSupplierId(),
            Name = request.Name.Trim(),
            Country = country,
            TaxId = taxId,
            Contacts = SupplierWriteRules.MapContacts(request.Contacts),
            KycOutcome = KycOutcome.Pending,
            SanctionsOutcome = SanctionsOutcome.Pending,
            Status = SupplierStatus.Active,
            OnboardingStatus = OnboardingStatus.Draft,
            PurchasingEnabled = true,          // K-4 (Revizyon 9): a new relationship is a purchasing relationship
            PurchasingSince = now,
            SourceSystem = request.SourceSystem,
            ExternalRef = request.ExternalRef,
            IdempotencyKey = key
        };

        try
        {
            var created = await store.CreateAsync(draft, cancellationToken);
            return Response<SupplierDetailDto>.Success(SupplierMapping.ToDetail(created), 201);
        }
        catch (SupplierDuplicateException ex) when (ex.IdempotencyRace && key is not null)
        {
            // K-7: the same key won the race — return the winner's record (the losing transaction wrote nothing, no event).
            var winner = await store.FindByIdempotencyKeyAsync(key, cancellationToken);
            return winner is null
                ? Response<SupplierDetailDto>.Fail("CONCURRENCY_CONFLICT", 409)
                : Response<SupplierDetailDto>.Success(SupplierMapping.ToDetail(winner), 201);
        }
        catch (SupplierDuplicateException ex)
        {
            // Unique index race on (tenant, country, normalized tax number): 409, never 500 (§12).
            return ex.ExistingSupplierId is { } code ? Duplicate(code) : Response<SupplierDetailDto>.Fail("DUPLICATE_SUPPLIER", 409);
        }
    }

    internal static Response<SupplierDetailDto> Duplicate(string existingSupplierId)
        => Response<SupplierDetailDto>.Fail("DUPLICATE_SUPPLIER", 409, new SupplierDuplicateDetails(existingSupplierId));
}
