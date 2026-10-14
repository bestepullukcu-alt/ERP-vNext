using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Commands;

/// <summary>POST /api/trace/lots (v1 <c>createLot</c> + v1.1 members). Raw request values; the handler owns parsing.</summary>
public sealed record CreateLotCommand : IRequest<Response<LotRefDto>>
{
    public string? HeaderIdempotencyKey { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? ItemId { get; init; }
    public string? LotNumber { get; init; }
    public string? ManufacturerLot { get; init; }
    public string? SupplierLot { get; init; }
    public string? ManufacturingDate { get; init; }
    public string? ExpiryDate { get; init; }
    public string? RetestDate { get; init; }
    public string? CountryOfOrigin { get; init; }
    public string? SkuId { get; init; }
    public string? SkuLevel { get; init; }
    public string? CoaReference { get; init; }
}
