using Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using InvoiceStatusEnum = Diten.ProcurementService.Domain.Entities.InvoiceStatus;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Handlers.CommandHandlers;

/// <summary>
/// Toplu soft delete. Public kod → iç Id çözümü tenant+LE filtreli; yalnız Captured olanlar silinir (eşleşmiş
/// atlanır). Silinen adedini döner. Hard delete YOK.
/// </summary>
public sealed class BulkDeleteInvoiceHandler : IRequestHandler<BulkDeleteInvoiceCommand, Response<int>>
{
    private readonly IInvoiceMatchRepository _repository;

    public BulkDeleteInvoiceHandler(IInvoiceMatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<int>> Handle(BulkDeleteInvoiceCommand request, CancellationToken cancellationToken)
    {
        var codes = (request.InvoiceIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (codes.Count == 0)
        {
            return Response<int>.Fail("No identifiers provided for bulk deletion.");
        }

        var capturedIds = new List<Guid>();
        foreach (var code in codes)
        {
            var entity = await _repository.GetByInvoiceIdAsync(code, cancellationToken);
            if (entity is not null && entity.Status == InvoiceStatusEnum.Captured)
            {
                capturedIds.Add(entity.Id);
            }
        }

        if (capturedIds.Count == 0)
        {
            return Response<int>.Success(0);
        }

        var deleted = await _repository.BulkDeleteAsync(capturedIds, cancellationToken);
        return Response<int>.Success(deleted);
    }
}
