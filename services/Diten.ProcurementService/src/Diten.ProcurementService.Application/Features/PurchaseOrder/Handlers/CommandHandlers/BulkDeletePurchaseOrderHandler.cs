using Diten.ProcurementService.Application.Features.PurchaseOrder.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using PoStatusEnum = Diten.ProcurementService.Domain.Entities.PoStatus;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Handlers.CommandHandlers;

/// <summary>
/// Toplu soft delete. Public kod → iç Id çözümü tenant+LE filtreli; yalnız Draft olanlar silinir (Draft dışı atlanır).
/// Silinen adedini döner. Hard delete YOK.
/// </summary>
public sealed class BulkDeletePurchaseOrderHandler : IRequestHandler<BulkDeletePurchaseOrderCommand, Response<int>>
{
    private readonly IPurchaseOrderRepository _repository;

    public BulkDeletePurchaseOrderHandler(IPurchaseOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<int>> Handle(BulkDeletePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var codes = (request.PoIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (codes.Count == 0)
        {
            return Response<int>.Fail("No identifiers provided for bulk deletion.");
        }

        // Yalnız Draft olanların iç Id'leri toplanır (tenant+LE filtreli; başka tenant/LE çözülmez → silinmez).
        var draftIds = new List<Guid>();
        foreach (var code in codes)
        {
            var entity = await _repository.GetByPoIdAsync(code, cancellationToken);
            if (entity is not null && entity.Status == PoStatusEnum.Draft)
            {
                draftIds.Add(entity.Id);
            }
        }

        if (draftIds.Count == 0)
        {
            return Response<int>.Success(0);
        }

        var deleted = await _repository.BulkDeleteAsync(draftIds, cancellationToken);
        return Response<int>.Success(deleted);
    }
}
