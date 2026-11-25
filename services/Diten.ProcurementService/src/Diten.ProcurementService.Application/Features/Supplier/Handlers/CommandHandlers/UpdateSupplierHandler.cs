using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Application.Features.Supplier.Store;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;

/// <summary>PATCH without <c>master.update</c> (§14, K-1) — relationship fields only under <c>TenantShared</c>.</summary>
public sealed class UpdateSupplierHandler(ISupplierStore store) : IRequestHandler<UpdateSupplierCommand, Response<SupplierDetailDto>>
{
    public async Task<Response<SupplierDetailDto>> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        // Cross-tenant / cross-LE / no relationship → 404 (UNKNOWN_SUPPLIER; cross-LE write engellenir).
        var current = await store.FindAsync(request.SupplierId, cancellationToken);
        if (current is null)
        {
            return Response<SupplierDetailDto>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        // ── Optimistic concurrency (If-Match/rowVersion): stale → 409, sessiz overwrite YOK ──
        if (request.ExpectedVersion.HasValue && request.ExpectedVersion.Value != current.Version)
        {
            return Response<SupplierDetailDto>.Fail("CONCURRENCY_CONFLICT", 409);
        }

        if (!store.IsTenantShared)
        {
            // PerLegalEntity: each company has its own record — the common fields change with `update`, as today (§14).
            return await SupplierMasterWrite.ApplyAsync(store, current, request.Name, request.Country, request.TaxId, request.Contacts,
                request.SourceSystem, request.ExternalRef, cancellationToken);
        }

        if (SupplierWriteRules.ChangesCommonFields(current, request.Name, request.Country, request.TaxId, request.Contacts))
        {
            return Response<SupplierDetailDto>.Fail("MASTER_UPDATE_FORBIDDEN", 403);
        }

        current.SourceSystem = request.SourceSystem;
        current.ExternalRef = request.ExternalRef;
        var saved = await store.SaveRelationshipAsync(current, cancellationToken);
        return saved is null
            ? Response<SupplierDetailDto>.Fail("CONCURRENCY_CONFLICT", 409)
            : Response<SupplierDetailDto>.Success(SupplierMapping.ToDetail(saved));
    }
}

/// <summary>PATCH with <c>master.update</c> (§14, K-1) — the common fields and the relationship fields.</summary>
public sealed class UpdateSupplierMasterHandler(ISupplierStore store) : IRequestHandler<UpdateSupplierMasterCommand, Response<SupplierDetailDto>>
{
    public async Task<Response<SupplierDetailDto>> Handle(UpdateSupplierMasterCommand request, CancellationToken cancellationToken)
    {
        var current = await store.FindAsync(request.SupplierId, cancellationToken);
        if (current is null)
        {
            return Response<SupplierDetailDto>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        if (request.ExpectedVersion.HasValue && request.ExpectedVersion.Value != current.Version)
        {
            return Response<SupplierDetailDto>.Fail("CONCURRENCY_CONFLICT", 409);
        }

        return await SupplierMasterWrite.ApplyAsync(store, current, request.Name, request.Country, request.TaxId, request.Contacts,
            request.SourceSystem, request.ExternalRef, cancellationToken);
    }
}

/// <summary>The common-field write both PATCH commands use: duplicate tax number → 409 naming the other supplier.</summary>
internal static class SupplierMasterWrite
{
    public static async Task<Response<SupplierDetailDto>> ApplyAsync(
        ISupplierStore store, SupplierSnapshot current, string name, string? country, string? taxIdRaw,
        List<SupplierContactInput>? contacts, string? sourceSystem, string? externalRef, CancellationToken ct)
    {
        var normalizedCountry = SupplierWriteRules.NormalizeCountry(country);
        var taxId = SupplierWriteRules.NormalizeTaxId(taxIdRaw);
        var taxKeyChanged = !string.Equals(taxId, current.TaxId, StringComparison.Ordinal)
                            || !string.Equals(normalizedCountry, current.Country, StringComparison.Ordinal);
        if (taxId is not null && taxKeyChanged
            && await store.FindDuplicateTaxIdAsync(normalizedCountry, taxId, current.MasterId, ct) is { } other)
        {
            return CreateSupplierHandler.Duplicate(other);
        }

        current.Name = name.Trim();
        current.Country = normalizedCountry;
        current.TaxId = taxId;
        current.Contacts = SupplierWriteRules.MapContacts(contacts);
        current.SourceSystem = sourceSystem;
        current.ExternalRef = externalRef;

        try
        {
            var saved = await store.SaveMasterAsync(current, ct);
            return saved is null
                ? Response<SupplierDetailDto>.Fail("CONCURRENCY_CONFLICT", 409)
                : Response<SupplierDetailDto>.Success(SupplierMapping.ToDetail(saved));
        }
        catch (SupplierDuplicateException ex)
        {
            return ex.ExistingSupplierId is { } code ? CreateSupplierHandler.Duplicate(code) : Response<SupplierDetailDto>.Fail("DUPLICATE_SUPPLIER", 409);
        }
    }
}
