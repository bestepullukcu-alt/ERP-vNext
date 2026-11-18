using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Queries;

/// <summary>WMS draft §4 <c>getLocationRole</c>.</summary>
public sealed record GetWarehouseLocationRoleByIdQuery(Guid RoleId) : IRequest<Response<WarehouseLocationRoleDto>>;
