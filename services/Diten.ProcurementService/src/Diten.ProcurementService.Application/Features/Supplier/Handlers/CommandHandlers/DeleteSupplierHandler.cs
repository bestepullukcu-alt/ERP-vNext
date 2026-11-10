using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Application.Features.Supplier.Store;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;

/// <summary>Soft-deletes the caller company's relationship (the supplier stays for the tenant's other companies).</summary>
public sealed class DeleteSupplierHandler(ISupplierStore store) : IRequestHandler<DeleteSupplierCommand, Response<bool>>
{
    public async Task<Response<bool>> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        // Cross-tenant / cross-LE / no relationship → 404 (cross-LE delete engellenir).
        var current = await store.FindAsync(request.SupplierId, cancellationToken);
        if (current is null || !await store.DeleteAsync(current, cancellationToken))
        {
            return Response<bool>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        return Response<bool>.Success(true);
    }
}

public sealed class BulkDeleteSupplierHandler(ISupplierStore store) : IRequestHandler<BulkDeleteSupplierCommand, Response<int>>
{
    public async Task<Response<int>> Handle(BulkDeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var codes = (request.SupplierIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (codes.Count == 0)
        {
            return Response<int>.Fail("No identifiers provided for bulk deletion.");
        }

        // Kod → ilişki çözümü tenant+LE filtreli (başka tenant/LE'nin kaydı çözülmez → silinmez).
        var found = await store.FindManyAsync(codes, cancellationToken);
        var targets = found.Values.GroupBy(s => s.RelationshipId).Select(g => g.First()).ToList();
        if (targets.Count == 0)
        {
            return Response<int>.Success(0);
        }

        return Response<int>.Success(await store.DeleteManyAsync(targets, cancellationToken));
    }
}
