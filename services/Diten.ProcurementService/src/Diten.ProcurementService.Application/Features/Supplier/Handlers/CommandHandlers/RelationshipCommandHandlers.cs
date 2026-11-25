using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Application.Features.Supplier.Store;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;

/// <summary>
/// 1.4.0 linkSupplierToCompany (K-1, K-6): the caller company relates to a supplier the tenant has. Only under
/// <c>TenantShared</c> — under <c>PerLegalEntity</c> there is no tenant-wide record to relate to (404, §8).
/// </summary>
public sealed class LinkSupplierToCompanyHandler(ISupplierStore store) : IRequestHandler<LinkSupplierToCompanyCommand, Response<SupplierRelationshipDto>>
{
    public async Task<Response<SupplierRelationshipDto>> Handle(LinkSupplierToCompanyCommand request, CancellationToken cancellationToken)
    {
        if (!store.IsTenantShared)
        {
            return Response<SupplierRelationshipDto>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;
        if (key is not null && await store.FindByIdempotencyKeyAsync(key, cancellationToken) is { } replay
            && string.Equals(replay.SupplierId, request.SupplierId, StringComparison.Ordinal))
        {
            return Response<SupplierRelationshipDto>.Success(SupplierMapping.ToRelationship(replay), 201);
        }

        var tenantSupplier = await store.FindTenantSupplierAsync(request.SupplierId, cancellationToken);
        if (tenantSupplier is null)
        {
            return Response<SupplierRelationshipDto>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        if (await store.FindAsync(tenantSupplier.Value.SupplierId, cancellationToken) is not null)
        {
            return Response<SupplierRelationshipDto>.Fail("RELATIONSHIP_EXISTS", 409);
        }

        try
        {
            var linked = await store.LinkAsync(tenantSupplier.Value.MasterId, key, cancellationToken);
            return Response<SupplierRelationshipDto>.Success(SupplierMapping.ToRelationship(linked), 201);
        }
        catch (SupplierRelationshipExistsException)
        {
            return Response<SupplierRelationshipDto>.Fail("RELATIONSHIP_EXISTS", 409);
        }
    }
}

/// <summary>1.4.0 changeRelationshipStatus — the caller company's relationship only; If-Match.</summary>
public sealed class ChangeRelationshipStatusHandler(ISupplierStore store) : IRequestHandler<ChangeRelationshipStatusCommand, Response<SupplierRelationshipDto>>
{
    public async Task<Response<SupplierRelationshipDto>> Handle(ChangeRelationshipStatusCommand request, CancellationToken cancellationToken)
    {
        var current = await RelationshipLookup.FindAsync(store, request.SupplierId, request.ExpectedVersion, cancellationToken);
        if (current.Failure is { } failure)
        {
            return failure;
        }

        current.Snapshot!.Status = request.Status;
        return await RelationshipLookup.SaveAsync(store, current.Snapshot, cancellationToken);
    }
}

/// <summary>1.4.0 setRelationshipPaymentTerms — a code only (K-8); the caller company's relationship; If-Match.</summary>
public sealed class SetRelationshipPaymentTermsHandler(ISupplierStore store) : IRequestHandler<SetRelationshipPaymentTermsCommand, Response<SupplierRelationshipDto>>
{
    public async Task<Response<SupplierRelationshipDto>> Handle(SetRelationshipPaymentTermsCommand request, CancellationToken cancellationToken)
    {
        var current = await RelationshipLookup.FindAsync(store, request.SupplierId, request.ExpectedVersion, cancellationToken);
        if (current.Failure is { } failure)
        {
            return failure;
        }

        current.Snapshot!.PaymentTermsCode = string.IsNullOrWhiteSpace(request.PaymentTermsCode) ? null : request.PaymentTermsCode.Trim();
        return await RelationshipLookup.SaveAsync(store, current.Snapshot, cancellationToken);
    }
}

internal static class RelationshipLookup
{
    public static async Task<(SupplierSnapshot? Snapshot, Response<SupplierRelationshipDto>? Failure)> FindAsync(
        ISupplierStore store, string supplierId, int? expectedVersion, CancellationToken ct)
    {
        if (!store.IsTenantShared)
        {
            return (null, Response<SupplierRelationshipDto>.Fail("UNKNOWN_SUPPLIER", 404));
        }

        var current = await store.FindAsync(supplierId, ct);
        if (current is null)
        {
            return (null, Response<SupplierRelationshipDto>.Fail("UNKNOWN_SUPPLIER", 404));
        }

        if (expectedVersion.HasValue && expectedVersion.Value != current.Version)
        {
            return (null, Response<SupplierRelationshipDto>.Fail("CONCURRENCY_CONFLICT", 409));
        }

        return (current, null);
    }

    public static async Task<Response<SupplierRelationshipDto>> SaveAsync(ISupplierStore store, SupplierSnapshot snapshot, CancellationToken ct)
    {
        var saved = await store.SaveRelationshipAsync(snapshot, ct);
        return saved is null
            ? Response<SupplierRelationshipDto>.Fail("CONCURRENCY_CONFLICT", 409)
            : Response<SupplierRelationshipDto>.Success(SupplierMapping.ToRelationship(saved));
    }
}
