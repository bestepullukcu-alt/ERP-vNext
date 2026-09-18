using Diten.ProcurementService.Application.Features.Grn.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using GrnStatusEnum = Diten.ProcurementService.Domain.Entities.GrnStatus;

namespace Diten.ProcurementService.Application.Features.Grn.Handlers.CommandHandlers;

/// <summary>
/// Toplu soft delete (ASSUMPTION-GRN-03). Public kod → iç Id çözümü tenant+LE filtreli; yalnız Draft olanlar
/// silinir (Posted atlanır). Silinen adedini döner. Hard delete YOK.
/// </summary>
public sealed class BulkDeleteGrnHandler : IRequestHandler<BulkDeleteGrnCommand, Response<int>>
{
    private readonly IGrnRepository _repository;

    public BulkDeleteGrnHandler(IGrnRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<int>> Handle(BulkDeleteGrnCommand request, CancellationToken cancellationToken)
    {
        var codes = (request.GrnIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (codes.Count == 0)
        {
            return Response<int>.Fail("No identifiers provided for bulk deletion.");
        }

        var draftIds = new List<Guid>();
        foreach (var code in codes)
        {
            var entity = await _repository.GetByGrnIdAsync(code, cancellationToken);
            if (entity is not null && entity.Status == GrnStatusEnum.Draft)
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
