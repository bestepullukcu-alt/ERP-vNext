using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.QueryHandlers;

/// <summary>getTransactions — immutable history of the caller's Tenant + LE, newest posting first.</summary>
public sealed class GetTransactionsHandler : IRequestHandler<GetTransactionsQuery, Response<TransactionPageDto>>
{
    private readonly IInventoryLedgerRepository _ledger;

    public GetTransactionsHandler(IInventoryLedgerRepository ledger) => _ledger = ledger;

    public async Task<Response<TransactionPageDto>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        DateTimeOffset? from = WireParsing.TryParseUtcDate(request.From, out var fromDay) ? fromDay : null;
        DateTimeOffset? toExclusive = WireParsing.TryParseUtcDate(request.To, out var toDay) ? toDay.AddDays(1) : null;
        var page = QueryParameterRules.Page(request.Page);
        var pageSize = QueryParameterRules.PageSize(request.PageSize);

        var result = await _ledger.QueryTransactionsAsync(new TransactionQuery(
            QueryParameterRules.Uuid(request.ItemId),
            QueryParameterRules.Text(request.LotId),
            QueryParameterRules.Text(request.LocationId),
            from,
            toExclusive,
            page,
            pageSize), cancellationToken);

        return Response<TransactionPageDto>.Success(new TransactionPageDto
        {
            Items = result.Items.Select(InventoryMapping.ToTransactionItem).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = result.Total
        });
    }
}
