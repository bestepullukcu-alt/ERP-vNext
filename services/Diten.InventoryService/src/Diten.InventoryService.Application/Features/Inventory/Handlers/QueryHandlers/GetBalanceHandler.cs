using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.QueryHandlers;

/// <summary>getBalance — projection rows of the caller's Tenant + LE only (read-only).</summary>
public sealed class GetBalanceHandler : IRequestHandler<GetBalanceQuery, Response<BalancePageDto>>
{
    private readonly IInventoryLedgerRepository _ledger;
    private readonly ITenantContext _tenantContext;

    public GetBalanceHandler(IInventoryLedgerRepository ledger, ITenantContext tenantContext)
    {
        _ledger = ledger;
        _tenantContext = tenantContext;
    }

    public async Task<Response<BalancePageDto>> Handle(GetBalanceQuery request, CancellationToken cancellationToken)
    {
        var page = await _ledger.QueryBalancesAsync(new BalanceQuery(
            QueryParameterRules.Uuid(request.ItemId),
            QueryParameterRules.Uuid(request.SkuId),
            QueryParameterRules.Level(request.SkuLevel),
            QueryParameterRules.Text(request.WarehouseId),
            QueryParameterRules.Text(request.LocationId),
            QueryParameterRules.Text(request.LotId),
            QueryParameterRules.Status(request.StockStatus),
            QueryParameterRules.Page(request.Page),
            QueryParameterRules.PageSize(request.PageSize)), cancellationToken);

        return Response<BalancePageDto>.Success(new BalancePageDto
        {
            Rows = page.Rows.Select(r => InventoryMapping.ToBalanceView(r, _tenantContext.LegalEntityId)).ToList(),
            Total = page.Total
        });
    }
}
