using Diten.ProcurementService.Application.Features.Contract.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using ContractEntity = Diten.ProcurementService.Domain.Entities.Contract;

namespace Diten.ProcurementService.Application.Features.Contract.Handlers.QueryHandlers;

/// <summary>listContracts (contract GET /api/contracts). Repository Tenant + LegalEntity + IsDeleted=false ile
/// filtreler (sızıntı yok); opsiyonel supplierId + status + cursor sayfalama (ContractId sıralı).</summary>
public sealed class ListContractsHandler : IRequestHandler<ListContractsQuery, Response<ContractListResultDto>>
{
    private const int PageSize = 50;

    private readonly IContractingRepository _repository;

    public ListContractsHandler(IContractingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ContractListResultDto>> Handle(ListContractsQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(request.SupplierId, request.Status, cancellationToken);

        var ordered = entities
            .OrderBy(x => x.ContractId, StringComparer.Ordinal)
            .ToList();

        IEnumerable<ContractEntity> page = ordered;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            page = ordered.Where(x => string.CompareOrdinal(x.ContractId, request.Cursor) > 0);
        }

        var pageList = page.Take(PageSize + 1).ToList();
        var hasMore = pageList.Count > PageSize;
        var items = pageList.Take(PageSize).Select(ContractMapping.ToDto).ToList();
        var nextCursor = hasMore ? pageList[PageSize - 1].ContractId : null;

        var result = new ContractListResultDto(items, nextCursor, ContractingContract.Version);
        return Response<ContractListResultDto>.Success(result);
    }
}
