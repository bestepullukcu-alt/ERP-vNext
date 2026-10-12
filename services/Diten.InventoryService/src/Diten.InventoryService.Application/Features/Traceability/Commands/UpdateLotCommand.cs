using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Commands;

/// <summary>
/// PATCH /api/trace/lots/{lotId} (v1.1 <c>updateLot</c>, K-C). Every attribute is a <see cref="Patch{T}"/>: absent =
/// unchanged, present null = cleared. The two permission flags come from the caller's token (the controller reads them);
/// which one is needed depends on the lot's status, which only the handler knows (Draft → update, else update-released).
/// </summary>
public sealed record UpdateLotCommand : IRequest<Response<LotRefDto>>
{
    public string? LotId { get; init; }
    public string? IfMatch { get; init; }
    public bool CanUpdateDraft { get; init; }
    public bool CanUpdateReleased { get; init; }
    public Patch<string> ManufacturerLot { get; init; }
    public Patch<string> SupplierLot { get; init; }
    public Patch<string> ManufacturingDate { get; init; }
    public Patch<string> ExpiryDate { get; init; }
    public Patch<string> RetestDate { get; init; }
    public Patch<string> CountryOfOrigin { get; init; }
    public Patch<string> CoaReference { get; init; }
    public string? Reason { get; init; }

    /// <summary>TRACE 1.4.0 (MOD-0176 KF-D, CT-F7): on a Draft lot, the override of dates that differ from the suggestion.</summary>
    public Ports.LotDateOverrideRef? DateOverride { get; init; }
}
