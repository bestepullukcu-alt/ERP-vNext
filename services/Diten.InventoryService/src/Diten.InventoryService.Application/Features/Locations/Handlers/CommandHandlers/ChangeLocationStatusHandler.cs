using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations.Commands;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Handlers.CommandHandlers;

/// <summary>
/// changeLocationStatus (MOD-0173-FU02 K-04…K-06). Active ⇄ Inactive; Active / Inactive → Archived (terminal).
/// <list type="bullet">
/// <item>Archived → anything: 409 <c>LOCATION_ARCHIVED</c>; stale ETag: 409 <c>LOCATION_CONFLICT</c>.</item>
/// <item>Deactivate / archive with stock on the node or below: 422 <c>LOCATION_HAS_STOCK</c>; with an Active child:
/// 422 <c>LOCATION_HAS_ACTIVE_CHILDREN</c> (K-05).</item>
/// <item>Activate under a parent that is not Active: 422 <c>LOCATION_PARENT_INACTIVE</c> (K-06).</item>
/// </list>
/// The checks run INSIDE the transaction that writes the node, and a ledger posting writes the same document — so a
/// posting racing a deactivation is a Mongo write conflict, and whichever re-runs sees the other's commit (§8).
/// </summary>
public sealed class ChangeLocationStatusHandler : IRequestHandler<ChangeLocationStatusCommand, Response<LocationDetailDto>>
{
    private readonly ILocationRepository _locations;
    private readonly LocationWriteSupport _support;

    public ChangeLocationStatusHandler(ILocationRepository locations, LocationWriteSupport support)
    {
        _locations = locations;
        _support = support;
    }

    public async Task<Response<LocationDetailDto>> Handle(ChangeLocationStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IfMatch))
            return LocationWriteSupport.Invalid<LocationDetailDto>("The If-Match header is required.");
        if (!LocationWriteSupport.TryParseStatus(request.Status, out var target))
            return LocationWriteSupport.Invalid<LocationDetailDto>("status is required: Active, Inactive or Archived.");
        if (!LocationWriteSupport.TryParseId(request.LocationId, out var locationId))
            return LocationWriteSupport.NotFound<LocationDetailDto>();

        var expectedVersion = LocationWriteSupport.ExpectedVersion(request.IfMatch);

        return await _locations.InTransactionAsync(async (tx, ct) =>
        {
            var node = await tx.FindAsync(locationId, ct);
            if (node is null) return LocationWriteSupport.NotFound<LocationDetailDto>();
            if (node.Status == LocationStatus.Archived)
                return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.Archived, "The location is archived; its status cannot change.", 409);
            if (node.Version != expectedVersion)
                return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.Conflict,
                    "The location changed since it was read; reload it and retry with its current ETag.", 409);
            if (node.Status == target)
                return Response<LocationDetailDto>.Success(LocationMapping.ToDetail(node), 200);

            if (target is LocationStatus.Inactive or LocationStatus.Archived)
            {
                if ((await tx.StockPresenceAsync(node, ct)).Any)
                    return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.HasStock,
                        "Stock is held at or below this location; move or issue it first.", 422);
                if (await tx.HasActiveChildAsync(node.Id, ct))
                    return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.HasActiveChildren,
                        "The location has active child locations; deactivate them first.", 422);
            }
            else if (node.ParentId is { } parentId)
            {
                var parent = await tx.FindAsync(parentId, ct);
                if (parent is null || parent.Status != LocationStatus.Active)
                    return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.ParentInactive,
                        "The parent location is not active; activate it first.", 422);
                await tx.TouchStructureAsync(parentId, ct);
            }

            var now = DateTimeOffset.UtcNow;
            var entry = _support.History(node.Id, LocationChangeAction.StatusChanged, "status", node.Status.ToString(), target.ToString(), now);
            node.Status = target;
            node.UpdatedAt = now;
            node.UpdatedBy = _support.UserId;
            if (!await tx.ReplaceAsync(node, expectedVersion, ct))
                return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.Conflict,
                    "The location changed since it was read; reload it and retry with its current ETag.", 409);
            await tx.AppendHistoryAsync([entry], ct);
            return Response<LocationDetailDto>.Success(LocationMapping.ToDetail(node), 200);
        }, cancellationToken);
    }
}
