using Diten.ProcurementService.Application.Features.Clause.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using ClauseEntity = Diten.ProcurementService.Domain.Entities.Clause;

namespace Diten.ProcurementService.Application.Features.Clause.Handlers.QueryHandlers;

/// <summary>listClauseLibrary (contract GET /api/contracts/clauses). Repository Tenant + LegalEntity + IsDeleted=false
/// ile filtreler (sızıntı yok); opsiyonel category + cursor sayfalama (ClauseId sıralı).</summary>
public sealed class ListClauseLibraryHandler : IRequestHandler<ListClauseLibraryQuery, Response<ClauseListResultDto>>
{
    private const int PageSize = 50;

    private readonly IContractingRepository _repository;

    public ListClauseLibraryHandler(IContractingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ClauseListResultDto>> Handle(ListClauseLibraryQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.ListClausesAsync(request.Category, cancellationToken);

        var ordered = entities
            .OrderBy(x => x.ClauseId, StringComparer.Ordinal)
            .ToList();

        IEnumerable<ClauseEntity> page = ordered;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            page = ordered.Where(x => string.CompareOrdinal(x.ClauseId, request.Cursor) > 0);
        }

        var pageList = page.Take(PageSize + 1).ToList();
        var hasMore = pageList.Count > PageSize;
        var items = pageList.Take(PageSize).Select(ClauseMapping.ToDto).ToList();
        var nextCursor = hasMore ? pageList[PageSize - 1].ClauseId : null;

        var result = new ClauseListResultDto(items, nextCursor, Contract.ContractingContract.Version);
        return Response<ClauseListResultDto>.Success(result);
    }
}
