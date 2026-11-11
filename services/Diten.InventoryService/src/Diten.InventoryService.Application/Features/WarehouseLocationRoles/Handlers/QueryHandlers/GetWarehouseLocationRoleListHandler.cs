using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Application.Features.WarehouseLocationRoles.Queries;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Handlers.QueryHandlers;

/// <summary>
/// <c>listLocationRoles</c> — the selected company's live records only (pack §8.1: another company's or tenant's record never shows), optional
/// warehouse (canonical UUID) and role (its exact name), page ≥ 1, pageSize ≤ 200; a malformed value is 400 <c>INVALID_REQUEST</c>.
/// </summary>
public sealed class GetWarehouseLocationRoleListHandler : IRequestHandler<GetWarehouseLocationRoleListQuery, Response<WarehouseLocationRolePageDto>>
{
    private readonly IWarehouseLocationRoleRepository _repository;
    private readonly LocationRoleReads _reads;

    public GetWarehouseLocationRoleListHandler(IWarehouseLocationRoleRepository repository, LocationRoleReads reads)
    {
        _repository = repository;
        _reads = reads;
    }

    public async Task<Response<WarehouseLocationRolePageDto>> Handle(GetWarehouseLocationRoleListQuery request, CancellationToken ct)
    {
        Guid? warehouseId = null;
        if (request.WarehouseId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.WarehouseId, out var parsed)) return WarehouseSupport.Invalid<WarehouseLocationRolePageDto>("warehouseId must be a UUID.");
            warehouseId = parsed;
        }

        WarehouseRole? role = null;
        if (request.Role is not null)
        {
            if (!Enum.TryParse<WarehouseRole>(request.Role, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed) || request.Role != parsed.ToString())
            {
                return WarehouseSupport.Invalid<WarehouseLocationRolePageDto>("role must be Receiving or Staging.");
            }

            role = parsed;
        }

        if (WarehousePaging.Parse(request.Page, request.PageSize) is not { } paging)
        {
            return WarehouseSupport.Invalid<WarehouseLocationRolePageDto>("page must be ≥ 1 and pageSize 1-200.");
        }

        var page = await _repository.ListAsync(new WarehouseLocationRoleQuery(warehouseId, role, paging.Page, paging.PageSize), ct);
        return Response<WarehouseLocationRolePageDto>.Success(
            new WarehouseLocationRolePageDto(await _reads.DtosAsync(page.Items, ct), page.Total, paging.Page, paging.PageSize));
    }
}
