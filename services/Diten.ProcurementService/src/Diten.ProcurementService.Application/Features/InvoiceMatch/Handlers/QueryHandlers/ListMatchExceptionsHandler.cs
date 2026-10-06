using Diten.ProcurementService.Application.Features.InvoiceMatch.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using MatchExceptionEntity = Diten.ProcurementService.Domain.Entities.MatchException;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Handlers.QueryHandlers;

/// <summary>listMatchExceptions (contract GET /api/invoice-match/exceptions). Repository Tenant + LegalEntity +
/// IsDeleted=false ile filtreler (sızıntı yok); opsiyonel reasonCode + cursor sayfalama (ExceptionId sıralı).</summary>
public sealed class ListMatchExceptionsHandler : IRequestHandler<ListMatchExceptionsQuery, Response<MatchExceptionListResultDto>>
{
    private const int PageSize = 50;

    private readonly IInvoiceMatchRepository _repository;

    public ListMatchExceptionsHandler(IInvoiceMatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<MatchExceptionListResultDto>> Handle(ListMatchExceptionsQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.ListExceptionsAsync(request.ReasonCode, cancellationToken);

        var ordered = entities
            .OrderBy(x => x.ExceptionId, StringComparer.Ordinal)
            .ToList();

        IEnumerable<MatchExceptionEntity> page = ordered;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            page = ordered.Where(x => string.CompareOrdinal(x.ExceptionId, request.Cursor) > 0);
        }

        var pageList = page.Take(PageSize + 1).ToList();
        var hasMore = pageList.Count > PageSize;
        var items = pageList.Take(PageSize).Select(InvoiceMatchMapping.ToExceptionDto).ToList();
        var nextCursor = hasMore ? pageList[PageSize - 1].ExceptionId : null;

        var result = new MatchExceptionListResultDto(items, nextCursor, InvoiceMatchContract.Version);
        return Response<MatchExceptionListResultDto>.Success(result);
    }
}
