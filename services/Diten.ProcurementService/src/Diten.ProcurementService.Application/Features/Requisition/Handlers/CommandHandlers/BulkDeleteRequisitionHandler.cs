using Diten.ProcurementService.Application.Features.Requisition.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using RequisitionStatusEnum = Diten.ProcurementService.Domain.Entities.RequisitionStatus;

namespace Diten.ProcurementService.Application.Features.Requisition.Handlers.CommandHandlers;

/// <summary>
/// Toplu soft delete. Public kod → iç Id çözümü tenant+LE filtreli; yalnız Draft olanlar silinir (Draft dışı atlanır).
/// Silinen adedini döner. Hard delete YOK.
/// </summary>
public sealed class BulkDeleteRequisitionHandler : IRequestHandler<BulkDeleteRequisitionCommand, Response<int>>
{
    private readonly IRequisitionRepository _repository;

    public BulkDeleteRequisitionHandler(IRequisitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<int>> Handle(BulkDeleteRequisitionCommand request, CancellationToken cancellationToken)
    {
        var codes = (request.RequisitionIds ?? new List<string>())
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
            var entity = await _repository.GetByRequisitionIdAsync(code, cancellationToken);
            if (entity is not null && entity.Status == RequisitionStatusEnum.Draft)
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
