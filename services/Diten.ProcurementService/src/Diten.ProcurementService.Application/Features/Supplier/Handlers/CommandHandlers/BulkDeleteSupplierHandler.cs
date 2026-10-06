using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Domain.Repositories;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;

public sealed class BulkDeleteSupplierHandler : IRequestHandler<BulkDeleteSupplierCommand, Response<int>>
{
    private readonly ISupplierRepository _repository;

    public BulkDeleteSupplierHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

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

        // Public kod → iç Id çözümü tenant+LE filtreli (başka tenant/LE'nin kaydı çözülmez → silinmez).
        var entities = await _repository.GetBySupplierIdsAsync(codes, cancellationToken);
        var ids = entities.Select(e => e.Id).ToList();
        if (ids.Count == 0)
        {
            return Response<int>.Success(0);
        }

        var deleted = await _repository.BulkDeleteAsync(ids, cancellationToken);
        return Response<int>.Success(deleted);
    }
}
