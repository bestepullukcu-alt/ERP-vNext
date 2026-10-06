using Diten.ProcurementService.Application.Features.Contract.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using ContractStatusEnum = Diten.ProcurementService.Domain.Entities.ContractStatus;

namespace Diten.ProcurementService.Application.Features.Contract.Handlers.CommandHandlers;

/// <summary>
/// Toplu soft delete (ASSUMPTION-0144-01 additive). Yalnız Draft olanlar silinir (aktive edilmiş/onaya gönderilmiş
/// sözleşmeler korunur); silinen adedini döner. Cross-tenant/LE görünmez (repository filtresi). Hard delete YOK.
/// </summary>
public sealed class BulkDeleteContractHandler : IRequestHandler<BulkDeleteContractCommand, Response<int>>
{
    private readonly IContractingRepository _repository;

    public BulkDeleteContractHandler(IContractingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<int>> Handle(BulkDeleteContractCommand request, CancellationToken cancellationToken)
    {
        var ids = request.ContractIds ?? new List<string>();
        var deletableIds = new List<Guid>();

        foreach (var contractId in ids.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var entity = await _repository.GetByContractIdAsync(contractId.Trim(), cancellationToken);
            if (entity is not null && entity.Status == ContractStatusEnum.Draft)
            {
                deletableIds.Add(entity.Id);
            }
        }

        var count = deletableIds.Count == 0
            ? 0
            : await _repository.BulkDeleteAsync(deletableIds, cancellationToken);

        return Response<int>.Success(count);
    }
}
