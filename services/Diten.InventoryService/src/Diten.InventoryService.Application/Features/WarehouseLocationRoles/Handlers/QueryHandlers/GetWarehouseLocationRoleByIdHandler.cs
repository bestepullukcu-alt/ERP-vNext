using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Application.Features.WarehouseLocationRoles.Queries;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Handlers.QueryHandlers;

/// <summary><c>getLocationRole</c> — a live record of the selected company; anything else (deleted, another company, another tenant) is 404.</summary>
public sealed class GetWarehouseLocationRoleByIdHandler : IRequestHandler<GetWarehouseLocationRoleByIdQuery, Response<WarehouseLocationRoleDto>>
{
    private readonly IWarehouseLocationRoleRepository _repository;
    private readonly LocationRoleReads _reads;

    public GetWarehouseLocationRoleByIdHandler(IWarehouseLocationRoleRepository repository, LocationRoleReads reads)
    {
        _repository = repository;
        _reads = reads;
    }

    public async Task<Response<WarehouseLocationRoleDto>> Handle(GetWarehouseLocationRoleByIdQuery request, CancellationToken ct)
        => await _repository.FindAsync(request.RoleId, ct) is { } role
            ? Response<WarehouseLocationRoleDto>.Success(await _reads.DtoAsync(role, ct))
            : WarehouseSupport.RoleNotFound<WarehouseLocationRoleDto>();
}
