using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Queries;

/// <summary>WMS draft §4 <c>listLocationRoles</c> — the raw query values; the handler parses them (400 on a malformed one).</summary>
public sealed record GetWarehouseLocationRoleListQuery(string? WarehouseId, string? Role, string? Page, string? PageSize)
    : IRequest<Response<WarehouseLocationRolePageDto>>;
