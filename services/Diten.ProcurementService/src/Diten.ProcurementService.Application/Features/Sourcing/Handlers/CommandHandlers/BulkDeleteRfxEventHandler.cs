using Diten.ProcurementService.Application.Features.Sourcing.Commands;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Handlers.CommandHandlers;

/// <summary>
/// Toplu soft delete. Public kod → iç Id çözümü tenant+LE filtreli; yalnız Draft olanlar silinir (Draft dışı atlanır).
/// Silinen adedini döner. Hard delete YOK.
/// </summary>
public sealed class BulkDeleteRfxEventHandler : IRequestHandler<BulkDeleteRfxEventCommand, Response<int>>
{
    private readonly IRfxRepository _repository;

    public BulkDeleteRfxEventHandler(IRfxRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<int>> Handle(BulkDeleteRfxEventCommand request, CancellationToken cancellationToken)
    {
        var codes = (request.RfxIds ?? new List<string>())
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
            var entity = await _repository.GetByRfxIdAsync(code, cancellationToken);
            if (entity is not null && entity.Status == RfxStatus.Draft)
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
